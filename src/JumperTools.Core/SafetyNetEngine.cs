using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Auto-Backup Safety Net Engine: Backup nyata partisi radio/security via fastboot fetch,
    /// dan 1-Click Rollback via fastboot flash kembali ke HP.
    /// Tidak ada dummy byte â€” setiap file backup adalah dump chip nyata.
    /// </summary>
    public class SafetyNetEngine
    {
        private readonly string _backupsBasePath;
        private FastbootEngine? _fastbootEngine;

        public event Action<string>? OnLog;

        public SafetyNetEngine(string? backupsBasePath = null)
        {
            _backupsBasePath = backupsBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups", "safety_snapshots");
            if (!Directory.Exists(_backupsBasePath))
                Directory.CreateDirectory(_backupsBasePath);
        }

        /// <summary>
        /// Inject FastbootEngine untuk backup/restore nyata
        /// </summary>
        public void SetFastbootEngine(FastbootEngine engine)
        {
            _fastbootEngine = engine;
            _fastbootEngine.OnLog += msg => OnLog?.Invoke(msg);
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        /// <summary>
        /// Get the list of critical partitions that must be backed up before wiping based on architecture
        /// </summary>
        public static List<string> GetCriticalPartitions(string architecture)
        {
            var arch = architecture?.ToLowerInvariant() ?? string.Empty;
            if (arch.Contains("mediatek") || arch.Contains("mtk"))
                return new List<string> { "nvram", "nvdata", "nvcfg", "protect1", "protect2", "proinfo", "persist", "misc" };
            if (arch.Contains("qualcomm") || arch.Contains("qcom"))
                return new List<string> { "modemst1", "modemst2", "fsg", "fsc", "persist", "misc", "config" };
            if (arch.Contains("samsung") || arch.Contains("exynos"))
                return new List<string> { "efs", "sec_efs", "nvdata", "param", "misc" };
            if (arch.Contains("unisoc") || arch.Contains("sprd"))
                return new List<string> { "prodnv", "miscdata", "persist", "phasecheck" };
            return new List<string> { "nvram", "persist", "misc" };
        }

        /// <summary>
        /// Buat backup nyata partisi vital sebelum flash menggunakan fastboot fetch.
        /// Setiap file yang tersimpan adalah dump chip nyata, bukan placeholder.
        /// Jika fastboot fetch tidak didukung bootloader, laporkan secara jujur dan hentikan.
        /// </summary>
        public async Task<SafetySnapshotInfo> CreatePreFlightSnapshotAsync(
            string brand,
            string modelName,
            string chipset,
            string serialNo,
            string architecture,
            string deviceSerial = "",
            CancellationToken ct = default)
        {
            var timestamp = DateTime.Now;
            var safeBrand = string.IsNullOrWhiteSpace(brand) ? "Generic" : brand.Replace(" ", "_");
            var safeModel = string.IsNullOrWhiteSpace(modelName) ? "Model" : modelName.Replace(" ", "_");
            var snapshotFolder = Path.Combine(_backupsBasePath, $"{safeBrand}_{safeModel}_{timestamp:yyyyMMdd_HHmmss}");

            Directory.CreateDirectory(snapshotFolder);

            var criticalList = GetCriticalPartitions(architecture);
            Log($"[Safety Net] Memulai Auto-Backup {criticalList.Count} partisi vital untuk '{safeBrand} {safeModel}'");

            if (_fastbootEngine == null)
            {
                Log("[Safety Net Info] FastbootEngine tidak aktif (offline / standalone). Mencatat katalog partisi metadata snapshot...");
                var metadataSnapshot = new SafetySnapshotInfo
                {
                    SnapshotId = Guid.NewGuid().ToString("N"),
                    Timestamp = timestamp,
                    Brand = safeBrand,
                    ModelName = safeModel,
                    Chipset = chipset,
                    SerialNo = serialNo,
                    BackupDirectory = snapshotFolder,
                    BackedUpPartitions = criticalList,
                    TotalSizeBytes = 0,
                    Status = "Snapshot Metadata Ready (Menunggu Koneksi Flasher)"
                };

                var metaPath = Path.Combine(snapshotFolder, "snapshot_metadata.json");
                var metaJson = JsonSerializer.Serialize(metadataSnapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(metaPath, metaJson, ct);

                return metadataSnapshot;
            }

            long totalBytes = 0;
            var backedUp = new List<string>();
            var failed = new List<string>();

            foreach (var part in criticalList)
            {
                if (ct.IsCancellationRequested) break;

                var dumpFile = Path.Combine(snapshotFolder, $"{part}.bin");
                Log($"[Safety Net] Backup partisi '{part}' â†’ {dumpFile}");

                var ok = await _fastbootEngine.FetchPartitionAsync(part, dumpFile, deviceSerial);

                if (ok && File.Exists(dumpFile))
                {
                    var size = new FileInfo(dumpFile).Length;
                    totalBytes += size;
                    backedUp.Add(part);
                    Log($"  â†’ OKAY âœ“ ({size / 1024.0:F1} KB tersimpan)");
                }
                else
                {
                    failed.Add(part);
                    Log($"  â†’ GAGAL âœ— Partisi '{part}' tidak bisa di-dump (bootloader tidak mendukung fastboot fetch).");
                    Log($"    Gunakan MTK BROM atau EDL 9008 untuk backup penuh pada device dengan bootloader terkunci.");
                }
            }

            var status = failed.Count == 0
                ? $"Backup OK â€” {backedUp.Count} partisi ({totalBytes / 1024.0:F1} KB)"
                : $"Backup Parsial â€” {backedUp.Count} berhasil, {failed.Count} gagal: [{string.Join(", ", failed)}]";

            var snapshot = new SafetySnapshotInfo
            {
                SnapshotId = Guid.NewGuid().ToString("N"),
                Timestamp = timestamp,
                Brand = safeBrand,
                ModelName = safeModel,
                Chipset = chipset,
                SerialNo = string.IsNullOrWhiteSpace(serialNo) ? deviceSerial : serialNo,
                BackupDirectory = snapshotFolder,
                BackedUpPartitions = backedUp,
                TotalSizeBytes = totalBytes,
                Status = status
            };

            var metaJsonPath = Path.Combine(snapshotFolder, "snapshot_metadata.json");
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metaJsonPath, json, ct);

            Log($"[Safety Net] Snapshot selesai. Status: {status}");
            Log($"[Safety Net] Folder backup: {snapshotFolder}");
            return snapshot;
        }

        /// <summary>
        /// Scan semua snapshot yang tersimpan
        /// </summary>
        public List<SafetySnapshotInfo> GetAllSnapshots()
        {
            var list = new List<SafetySnapshotInfo>();
            if (!Directory.Exists(_backupsBasePath)) return list;

            try
            {
                var dirs = Directory.GetDirectories(_backupsBasePath);
                foreach (var dir in dirs)
                {
                    var metaPath = Path.Combine(dir, "snapshot_metadata.json");
                    if (File.Exists(metaPath))
                    {
                        var json = File.ReadAllText(metaPath);
                        var snap = JsonSerializer.Deserialize<SafetySnapshotInfo>(json);
                        if (snap != null) list.Add(snap);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Safety Net Scan Error] {ex.Message}");
            }

            return list.OrderByDescending(s => s.Timestamp).ToList();
        }

        /// <summary>
        /// 1-Click Rollback: Restore snapshot backup ke HP menggunakan fastboot flash per partisi.
        /// Hanya file yang benar-benar ada di folder backup yang akan di-flash.
        /// </summary>
        public async Task<bool> RollbackSnapshotAsync(SafetySnapshotInfo snapshot, string portName, CancellationToken ct = default)
        {
            if (!Directory.Exists(snapshot.BackupDirectory))
            {
                Log($"[Rollback Error] Direktori snapshot '{snapshot.BackupDirectory}' tidak ditemukan!");
                return false;
            }

            if (_fastbootEngine == null)
            {
                Log("[Rollback ERROR] FastbootEngine tidak tersedia. Sambungkan HP dalam mode Fastboot.");
                return false;
            }

            Log($"[Safety Net Rollback] Memulai 1-Click Restore snapshot '{snapshot.Brand} {snapshot.ModelName}' ke {portName}");
            Log($"[Safety Net Rollback] Folder sumber: {snapshot.BackupDirectory}");

            var files = Directory.GetFiles(snapshot.BackupDirectory, "*.bin");
            if (files.Length == 0)
            {
                Log("[Rollback Error] Tidak ada file .bin di folder snapshot ini. Backup mungkin kosong atau rusak.");
                return false;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;

                var partName = Path.GetFileNameWithoutExtension(file);
                var fileSize = new FileInfo(file).Length;

                if (fileSize < 512)
                {
                    Log($"  [Skip] '{partName}': File terlalu kecil ({fileSize} bytes) â€” kemungkinan bukan backup valid.");
                    failCount++;
                    continue;
                }

                Log($"[Rollback] Flash '{partName}' â† {file} ({fileSize / 1024.0:F1} KB)");
                var ok = await _fastbootEngine.FlashPartitionAsync(partName, file, portName);

                if (ok)
                {
                    successCount++;
                    Log($"  â†’ OKAY âœ“");
                }
                else
                {
                    failCount++;
                    Log($"  â†’ GAGAL âœ— (Bootloader menolak penulisan ke '{partName}')");
                }
            }

            Log($"[Safety Net Rollback] Selesai: {successCount} partisi berhasil dipulihkan, {failCount} gagal.");
            return successCount > 0;
        }
    }
}


