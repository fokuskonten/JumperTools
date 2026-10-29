using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Multi-Format Firmware Flashing Engine — disambungkan 100% ke FastbootEngine real.
    /// Mendukung MediaTek Scatter, Qualcomm RawProgram XML, Samsung 4-File Odin TAR, Unisoc PAC.
    /// Tidak ada simulasi. Setiap partisi dikirim langsung ke chip HP melalui fastboot flash.
    /// </summary>
    public class FirmwareFlasherEngine : IFirmwareFlasherProtocol
    {
        private readonly PartitionManagerEngine _partitionManager;
        private FastbootEngine? _fastbootEngine;

        public Action<string>? OnLog { get; set; }
        public Action<double>? OnProgress { get; set; }
        public Action<string, double>? OnPartitionFlashingProgress { get; set; }

        public FirmwareFlasherEngine(PartitionManagerEngine? partitionManager = null)
        {
            _partitionManager = partitionManager ?? new PartitionManagerEngine();
        }

        public async Task<bool> FlashFirmwareAsync(string firmwarePath, string targetDeviceOrPort, CancellationToken cancellationToken = default)
        {
            var manifest = AnalyzeFirmwarePackage(firmwarePath);
            return await ExecuteFlashingAsync(manifest, targetDeviceOrPort, cancellationToken);
        }

        public async Task<bool> EraseFrpAsync(string targetDeviceOrPort, CancellationToken cancellationToken = default)
        {
            if (_fastbootEngine == null) return false;
            return await _fastbootEngine.ErasePartitionAsync("frp", targetDeviceOrPort);
        }

        public async Task<bool> FactoryResetAsync(string targetDeviceOrPort, CancellationToken cancellationToken = default)
        {
            if (_fastbootEngine == null) return false;
            var okData = await _fastbootEngine.ErasePartitionAsync("userdata", targetDeviceOrPort);
            var okCache = await _fastbootEngine.ErasePartitionAsync("cache", targetDeviceOrPort);
            return okData || okCache;
        }

        /// <summary>
        /// Inject FastbootEngine untuk operasi flash nyata
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
        /// Auto-detect firmware package type and analyze its manifest
        /// </summary>
        public FirmwarePackageManifest AnalyzeFirmwarePackage(string filePathOrDir)
        {
            var manifest = new FirmwarePackageManifest();

            if (File.Exists(filePathOrDir))
            {
                var ext = Path.GetExtension(filePathOrDir).ToLowerInvariant();
                var dir = Path.GetDirectoryName(filePathOrDir) ?? string.Empty;
                manifest.PackageDirectory = dir;
                manifest.PackageName = Path.GetFileName(filePathOrDir);

                if (ext == ".txt" && filePathOrDir.Contains("scatter", StringComparison.OrdinalIgnoreCase))
                {
                    manifest.PackageType = FirmwarePackageType.MediaTekScatter;
                    var content = File.ReadAllText(filePathOrDir);
                    manifest.Items = _partitionManager.ParseMediaTekScatter(content);
                    manifest.TargetChipset = "MediaTek (Helio / Dimensity)";
                }
                else if (ext == ".xml" && filePathOrDir.Contains("rawprogram", StringComparison.OrdinalIgnoreCase))
                {
                    manifest.PackageType = FirmwarePackageType.QualcommRawProgramXml;
                    var content = File.ReadAllText(filePathOrDir);
                    manifest.Items = _partitionManager.ParseQualcommRawProgramXml(content);
                    manifest.TargetChipset = "Qualcomm Snapdragon";
                }
                else if (ext == ".tar" || ext == ".md5")
                {
                    // PERBAIKAN P5-1: Jangan hardcode partisi/ukuran — scan file nyata di folder.
                    // Samsung TAR.MD5 berisi file BL/AP/CP/CSC yang berbeda per firmware version.
                    manifest.PackageType = FirmwarePackageType.SamsungOdinTar;
                    manifest.TargetChipset = "Samsung Exynos / Qualcomm (Odin)";
                    var tarDir = Path.GetDirectoryName(filePathOrDir) ?? string.Empty;
                    var tarFiles = new[] { "BL_", "AP_", "CP_", "CSC_", "HOME_CSC_", "boot", "recovery", "system", "vbmeta" };
                    var idx = 0;
                    foreach (var prefix in tarFiles)
                    {
                        var found = Directory.GetFiles(tarDir, $"{prefix}*.tar*")
                                             .Concat(Directory.GetFiles(tarDir, $"{prefix}*.md5"))
                                             .FirstOrDefault();
                        if (string.IsNullOrEmpty(found)) continue;
                        var fi = new FileInfo(found);
                        var partName = prefix.TrimEnd('_');
                        manifest.Items.Add(new PartitionTableItem
                        {
                            Index = idx++,
                            PartitionName = partName,
                            FileName = fi.Name,
                            SizeBytes = fi.Length,
                            SizeFormatted = fi.Length > 1024 * 1024 ? $"{fi.Length / (1024.0 * 1024):F1} MB" : $"{fi.Length / 1024:N0} KB",
                            IsSelected = true
                        });
                    }
                    // Fallback jika tidak ada file spesifik: tampilkan file TAR itu sendiri
                    if (manifest.Items.Count == 0)
                    {
                        var fi = new FileInfo(filePathOrDir);
                        manifest.Items.Add(new PartitionTableItem
                        {
                            Index = 0, PartitionName = "FIRMWARE", FileName = fi.Name,
                            SizeBytes = fi.Length,
                            SizeFormatted = fi.Length > 1024 * 1024 ? $"{fi.Length / (1024.0 * 1024):F1} MB" : $"{fi.Length / 1024:N0} KB",
                            IsSelected = true
                        });
                    }
                }
                else if (ext == ".pac")
                {
                    // PERBAIKAN P5-1: PAC adalah single archive — tampilkan sebagai 1 entry dengan ukuran nyata.
                    // Parsing internal PAC header untuk daftar partisi memerlukan UnisocSpdEngine.
                    manifest.PackageType = FirmwarePackageType.UnisocPac;
                    manifest.TargetChipset = "Unisoc / Spreadtrum Tiger";
                    var fi = new FileInfo(filePathOrDir);
                    manifest.Items.Add(new PartitionTableItem
                    {
                        Index = 0,
                        PartitionName = "PAC_FIRMWARE",
                        FileName = fi.Name,
                        SizeBytes = fi.Length,
                        SizeFormatted = fi.Length > 1024 * 1024 ? $"{fi.Length / (1024.0 * 1024):F1} MB" : $"{fi.Length / 1024:N0} KB",
                        IsSelected = true
                    });
                }
            }
            else if (Directory.Exists(filePathOrDir))
            {
                manifest.PackageDirectory = filePathOrDir;
                manifest.PackageName = Path.GetFileName(filePathOrDir);

                var scatterFile = Directory.GetFiles(filePathOrDir, "*scatter*.txt").FirstOrDefault();
                var xmlFile = Directory.GetFiles(filePathOrDir, "rawprogram*.xml").FirstOrDefault();

                if (scatterFile != null) return AnalyzeFirmwarePackage(scatterFile);
                if (xmlFile != null) return AnalyzeFirmwarePackage(xmlFile);
            }

            manifest.TotalPackageSizeBytes = manifest.Items.Sum(i => i.SizeBytes);
            manifest.IsValid = manifest.Items.Count > 0;
            manifest.ValidationMessage = manifest.IsValid
                ? $"Siap Flash ({manifest.Items.Count} Partisi, Total {manifest.TotalPackageSizeFormatted})"
                : "Format ROM tidak dikenali atau kosong";

            return manifest;
        }

        /// <summary>
        /// Flash multi-partisi ke HP melalui fastboot flash (REAL — setiap partisi ditulis langsung ke chip).
        /// Memerlukan HP terhubung dalam mode Fastboot dengan driver WinUSB terpasang.
        /// Setiap partisi divalidasi: file harus ada + bootloader harus membalas OKAY.
        /// </summary>
        public async Task<bool> ExecuteFlashingAsync(
            FirmwarePackageManifest manifest,
            string portName,
            CancellationToken ct = default)
        {
            if (_fastbootEngine == null)
            {
                Log("[ERROR] FastbootEngine belum tersedia.");
                Log("[ERROR] Sambungkan HP dalam mode Fastboot dan pastikan driver WinUSB sudah terpasang (Tab Driver → Install LibUSB).");
                return false;
            }

            var selectedItems = manifest.Items.Where(i => i.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                Log("[Flasher Error] Tidak ada partisi yang dipilih untuk di-flash!");
                return false;
            }

            Log($"[Flasher] ══════════════════════════════════════════════════════");
            Log($"[Flasher] Memulai Flashing ROM '{manifest.PackageName}'");
            Log($"[Flasher] Target: {selectedItems.Count} partisi ke perangkat {portName}");
            Log($"[Flasher] Chipset: {manifest.TargetChipset}");
            Log($"[Flasher] ══════════════════════════════════════════════════════");

            double progressStep = 100.0 / selectedItems.Count;
            double currentProgress = 0;
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < selectedItems.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    Log("[Flasher] Proses flashing dibatalkan oleh pengguna.");
                    return false;
                }

                var item = selectedItems[i];
                OnPartitionFlashingProgress?.Invoke(item.PartitionName, 0);

                // Cari file image di direktori manifest
                var imageFile = string.Empty;
                if (!string.IsNullOrEmpty(manifest.PackageDirectory))
                {
                    var candidates = new[]
                    {
                        Path.Combine(manifest.PackageDirectory, item.FileName ?? string.Empty),
                        Path.Combine(manifest.PackageDirectory, item.PartitionName + ".img"),
                        Path.Combine(manifest.PackageDirectory, item.PartitionName + ".bin"),
                    };
                    imageFile = candidates.FirstOrDefault(f => !string.IsNullOrEmpty(f) && File.Exists(f)) ?? string.Empty;
                }

                if (string.IsNullOrEmpty(imageFile))
                {
                    Log($"[{i + 1}/{selectedItems.Count}] ⚠️  Lewati '{item.PartitionName}': File image tidak ditemukan di folder ROM.");
                    failCount++;
                    OnPartitionFlashingProgress?.Invoke(item.PartitionName, -1); // -1 = skip
                    currentProgress += progressStep;
                    OnProgress?.Invoke(Math.Min(100, currentProgress));
                    continue;
                }

                var fileInfo = new FileInfo(imageFile);

                // PERBAIKAN P5-2: Validasi ukuran file > 0 sebelum flash.
                // File partial download atau file corrupt berukuran 0-byte akan ditolak eksplisit.
                if (fileInfo.Length == 0)
                {
                    Log($"[{i + 1}/{selectedItems.Count}] ⛔ Lewati '{item.PartitionName}': File '{fileInfo.Name}' berukuran 0-byte — kemungkinan download tidak lengkap atau file corrupt.");
                    failCount++;
                    OnPartitionFlashingProgress?.Invoke(item.PartitionName, -1);
                    currentProgress += progressStep;
                    OnProgress?.Invoke(Math.Min(100, currentProgress));
                    continue;
                }

                Log($"[{i + 1}/{selectedItems.Count}] Flash '{item.PartitionName}' ← {fileInfo.Name} ({fileInfo.Length / (1024.0 * 1024):F1} MB)");
                OnPartitionFlashingProgress?.Invoke(item.PartitionName, 10);

                var ok = await _fastbootEngine.FlashPartitionAsync(item.PartitionName, imageFile, portName);

                if (ok)
                {
                    successCount++;
                    OnPartitionFlashingProgress?.Invoke(item.PartitionName, 100);
                    Log($"  → OKAY ✓");
                }
                else
                {
                    failCount++;
                    OnPartitionFlashingProgress?.Invoke(item.PartitionName, -1);
                    Log($"  → GAGAL ✗ (Bootloader menolak atau file tidak kompatibel)");
                }

                currentProgress += progressStep;
                OnProgress?.Invoke(Math.Min(100, currentProgress));
            }

            OnProgress?.Invoke(100);
            Log($"[Flasher] ══════════════════════════════════════════════════════");
            Log($"[Flasher] Flashing Selesai: {successCount} partisi OKAY, {failCount} partisi gagal/dilewati.");

            if (failCount == 0)
                Log($"[Flasher] ✓ Semua partisi berhasil diflash. Silakan reboot HP.");
            else
                Log($"[Flasher] ⚠️  {failCount} partisi gagal. Periksa log di atas untuk detail per partisi.");

            return successCount > 0;
        }
    }
}


