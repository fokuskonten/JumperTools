using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Interactive Partition Table Manager for eMMC, UFS, and GPT direct operations (Read, Erase, Write)
    /// </summary>
    public class PartitionManagerEngine : IPartitionIOEngine
    {
        private readonly string _toolsBasePath;
        private readonly ToolExecutionEngine _toolEngine;

        public event Action<string>? OnLog;
        public event Action<double>? OnProgress;

        public PartitionManagerEngine(
            string? toolsBasePath = null,
            FastbootEngine? fastbootEngine = null,
            ToolExecutionEngine? toolEngine = null,
            object? backupIntegrityService = null)
        {
            _toolsBasePath = toolsBasePath ?? AppPathResolver.ToolsDirectory;
            _toolEngine = toolEngine ?? new ToolExecutionEngine(_toolsBasePath);
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        /// <summary>
        /// Determine the risk level of a partition based on standard GSM baseband partitions
        /// </summary>
        public static PartitionRiskLevel ClassifyPartitionRisk(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return PartitionRiskLevel.Safe;
            var lower = name.ToLowerInvariant().Trim();

            var critical = new HashSet<string>
            {
                "nvram", "nvdata", "nvcfg", "proinfo", "protect1", "protect2", "protect_f", "protect_s",
                "modemst1", "modemst2", "fsg", "fsc", "persist", "efs", "sec_efs", "carrier", "param",
                "rpmb", "secro", "seccfg", "md1img", "md1dsp", "spmfw", "xbl", "xbl_config", "xbl_a", "xbl_b",
                "preloader", "preloader_a", "preloader_b", "abl", "abl_a", "abl_b", "tz", "rpm", "sbl1",
                "aboot", "keystore", "devinfo", "frp", "config"
            };

            var warning = new HashSet<string>
            {
                "super", "system", "vendor", "product", "odm", "system_ext", "cust", "my_manifest",
                "boot", "init_boot", "vendor_boot", "recovery", "dtbo", "vbmeta", "vbmeta_system", "vbmeta_vendor"
            };

            if (critical.Contains(lower) || lower.StartsWith("modem") || lower.StartsWith("nv") || lower.StartsWith("efs") || lower.StartsWith("xbl"))
                return PartitionRiskLevel.CriticalRadio;

            if (warning.Contains(lower))
                return PartitionRiskLevel.Warning;

            return PartitionRiskLevel.Safe;
        }

        /// <summary>
        /// Check whether a target partition is a sensitive radio, baseband, or security partition
        /// </summary>
        public static bool IsSensitiveSecurityPartition(string name)
        {
            return ClassifyPartitionRisk(name) == PartitionRiskLevel.CriticalRadio;
        }

        /// <summary>
        /// Real live partition reader across ADB (Android OS), Fastboot, and MediaTek BROM.
        /// Zero mock/dummy data - returns real hardware partition table or honest failure.
        /// </summary>
        public async Task<List<PartitionTableItem>> ReadLivePartitionTableAsync(
            string? deviceSerial,
            string? protocol = null,
            string? portName = null,
            CancellationToken ct = default)
        {
            var list = new List<PartitionTableItem>();
            var protoStr = protocol ?? "Auto";
            Log($"[Partition Manager] Membaca tabel partisi perangkat secara nyata via {protoStr}...");

            var devId = (string.IsNullOrEmpty(deviceSerial) || deviceSerial == "ADB_DEV" || deviceSerial == "FASTBOOT_DEV") ? "" : deviceSerial;

            if (protoStr.Contains("ADB", StringComparison.OrdinalIgnoreCase) || protoStr.Contains("Normal", StringComparison.OrdinalIgnoreCase) || protoStr.Contains("AdbRoot", StringComparison.OrdinalIgnoreCase))
            {
                // 1. Read real block device symlinks from Android
                var lsRes = await _toolEngine.ExecuteAdbCommandAsync(devId, "shell \"ls -l /dev/block/by-name/ 2>/dev/null || ls -l /dev/block/bootdevice/by-name/ 2>/dev/null || ls -l /dev/block/platform/*/by-name/ 2>/dev/null\"");
                
                // 2. Read /proc/partitions for exact block sizes
                var procRes = await _toolEngine.ExecuteAdbCommandAsync(devId, "shell cat /proc/partitions");

                var lsResult = lsRes.Output ?? "";
                var procResult = procRes.Output ?? "";

                if (lsResult.Contains("no devices", StringComparison.OrdinalIgnoreCase) || 
                    lsResult.Contains("device offline", StringComparison.OrdinalIgnoreCase) || 
                    lsResult.Contains("device unauthorized", StringComparison.OrdinalIgnoreCase) ||
                    lsResult.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    Log("[GAGAL] Perangkat tidak terdeteksi via ADB atau otorisasi USB Debugging belum disetujui di layar HP.");
                    return list;
                }

                var partitionSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(procResult))
                {
                    var lines = procResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 4 && long.TryParse(parts[2], out var blocks))
                        {
                            var devName = parts[3].Trim();
                            partitionSizes[devName] = blocks * 1024L; // 1KB blocks
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(lsResult))
                {
                    var lines = lsResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int idx = 0;
                    foreach (var line in lines)
                    {
                        // PERBAIKAN Cacat P3-4: Normalisasi symlink path sebelum lookup.
                        // ls -l by-name output bisa menggunakan path relatif seperti:
                        //   boot -> ../../mmcblk0p28
                        //   modemst1 -> /dev/block/platform/soc.0/by-name/modemst1
                        // Path.GetFileName() pada keduanya menghasilkan nama block device yang konsisten.
                        var match = Regex.Match(line, @"([a-zA-Z0-9_\-\.]+)\s*->\s*([^\s]+)");
                        if (match.Success)
                        {
                            var partName = match.Groups[1].Value.Trim();
                            // Normalisasi: ambil nama block device terakhir dari path (absolut atau relatif)
                            var rawTarget = match.Groups[2].Value.Trim();
                            var targetDev = Path.GetFileName(rawTarget.Replace('/', Path.DirectorySeparatorChar));
                            
                            long sizeBytes = 0;
                            if (partitionSizes.TryGetValue(targetDev, out var sz))
                            {
                                sizeBytes = sz;
                            }

                            var sizeFormatted = sizeBytes >= 1073741824 
                                ? $"{sizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB" 
                                : sizeBytes >= 1048576 
                                    ? $"{sizeBytes / (1024.0 * 1024.0):F1} MB" 
                                    : sizeBytes > 0 
                                        ? $"{sizeBytes / 1024.0:F0} KB" 
                                        : "-";

                            list.Add(new PartitionTableItem
                            {
                                Index = idx++,
                                PartitionName = partName,
                                FileName = $"{partName}.img",
                                StartSector = 0,
                                SectorCount = sizeBytes > 0 ? sizeBytes / 512 : 0,
                                SizeBytes = sizeBytes,
                                SizeFormatted = sizeFormatted,
                                Flags = targetDev,
                                RiskLevel = ClassifyPartitionRisk(partName),
                                IsSelected = true
                            });
                        }
                    }
                }

                if (list.Count == 0 && partitionSizes.Count > 0)
                {
                    Log("[INFO] Akses direct block by-name dibatasi oleh SELinux Android. Membaca daftar partisi dari /proc/partitions...");
                    int idx = 0;
                    foreach (var kvp in partitionSizes.Where(k => !k.Key.StartsWith("loop") && !k.Key.StartsWith("ram")))
                    {
                        var sizeBytes = kvp.Value;
                        var sizeFormatted = sizeBytes >= 1073741824 
                            ? $"{sizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB" 
                            : $"{sizeBytes / (1024.0 * 1024.0):F1} MB";

                        list.Add(new PartitionTableItem
                        {
                            Index = idx++,
                            PartitionName = kvp.Key,
                            FileName = $"{kvp.Key}.img",
                            SizeBytes = sizeBytes,
                            SizeFormatted = sizeFormatted,
                            Flags = "BlockDev",
                            RiskLevel = ClassifyPartitionRisk(kvp.Key),
                            IsSelected = true
                        });
                    }
                }
            }
            else if (protoStr.Contains("MediaTek", StringComparison.OrdinalIgnoreCase) || protoStr.Contains("BROM", StringComparison.OrdinalIgnoreCase))
            {
                Log("[MediaTek BROM] Membaca GPT fisik eMMC/UFS via mtkclient...");
                var res = await _toolEngine.ExecuteMtkClientAsync("printgpt", Log);
                if (res.Success && !string.IsNullOrWhiteSpace(res.Output))
                {
                    var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int idx = 0;
                    foreach (var line in lines)
                    {
                        var match = Regex.Match(line, @"Partition\s+""?([a-zA-Z0-9_\-\.]+)""?:\s*Offset\s+0x([0-9a-fA-F]+),\s*Length\s+0x([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var name = match.Groups[1].Value;
                            var offset = Convert.ToInt64(match.Groups[2].Value, 16);
                            var len = Convert.ToInt64(match.Groups[3].Value, 16);
                            var sizeFormatted = len >= 1073741824 ? $"{len / 1073741824.0:F2} GB" : $"{len / 1048576.0:F1} MB";

                            list.Add(new PartitionTableItem
                            {
                                Index = idx++,
                                PartitionName = name,
                                FileName = $"{name}.bin",
                                StartSector = offset / 512,
                                SectorCount = len / 512,
                                SizeBytes = len,
                                SizeFormatted = sizeFormatted,
                                Flags = "GPT eMMC/UFS",
                                RiskLevel = ClassifyPartitionRisk(name),
                                IsSelected = true
                            });
                        }
                    }
                }
                else
                {
                    Log($"[GAGAL] Pembacaan GPT MediaTek gagal: {res.ErrorMessage}");
                }
            }
            else if (protoStr.Contains("Fastboot", StringComparison.OrdinalIgnoreCase))
            {
                var fbRes = await _toolEngine.ExecuteFastbootCommandAsync(devId, "getvar all");
                var fbResult = fbRes.Output ?? "";
                if (!string.IsNullOrWhiteSpace(fbResult))
                {
                    var lines = fbResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int idx = 0;
                    foreach (var line in lines)
                    {
                        var match = Regex.Match(line, @"partition-size:([a-zA-Z0-9_\-\.]+):\s*0x([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var name = match.Groups[1].Value;
                            var len = Convert.ToInt64(match.Groups[2].Value, 16);
                            var sizeFormatted = len >= 1073741824 ? $"{len / 1073741824.0:F2} GB" : $"{len / 1048576.0:F1} MB";

                            list.Add(new PartitionTableItem
                            {
                                Index = idx++,
                                PartitionName = name,
                                FileName = $"{name}.img",
                                SizeBytes = len,
                                SizeFormatted = sizeFormatted,
                                Flags = "Fastboot GPT",
                                RiskLevel = ClassifyPartitionRisk(name),
                                IsSelected = true
                            });
                        }
                    }
                }
            }

            Log(list.Count > 0 
                ? $"[SUKSES] Berhasil membaca {list.Count} partisi fisik nyata dari perangkat." 
                : $"[PERINGATAN] Tidak ada partisi yang berhasil dibaca. Pastikan mode koneksi hardware ({protocol}) sesuai.");

            return list;
        }

        /// <summary>
        /// Get standard default partition table template for general Android devices
        /// </summary>
        public List<PartitionTableItem> GetDefaultPartitionTable()
        {
            var defaultPartitions = new[]
            {
                ("boot", 67108864L, "boot.img"),
                ("recovery", 67108864L, "recovery.img"),
                ("vbmeta", 8192L, "vbmeta.img"),
                ("vbmeta_system", 8192L, "vbmeta_system.img"),
                ("dtbo", 8388608L, "dtbo.img"),
                ("persist", 33554432L, "persist.img"),
                ("nvram", 5242880L, "nvram.bin"),
                ("nvdata", 33554432L, "nvdata.bin"),
                ("frp", 1048576L, "frp.bin"),
                ("modemst1", 2097152L, "modemst1.bin"),
                ("modemst2", 2097152L, "modemst2.bin"),
                ("fsg", 2097152L, "fsg.bin"),
                ("misc", 1048576L, "misc.bin"),
                ("super", 4294967296L, "super.img"),
                ("userdata", 8589934592L, "userdata.img")
            };

            int idx = 0;
            return defaultPartitions.Select(p => new PartitionTableItem
            {
                Index = idx++,
                PartitionName = p.Item1,
                FileName = p.Item3,
                SizeBytes = p.Item2,
                SizeFormatted = p.Item2 >= 1073741824 ? $"{p.Item2 / 1073741824.0:F1} GB" : $"{p.Item2 / 1048576.0:F1} MB",
                RiskLevel = ClassifyPartitionRisk(p.Item1),
                IsSelected = true
            }).ToList();
        }

        /// <summary>
        /// Parse Qualcomm rawprogram0.xml file into structured partition items
        /// </summary>
        public List<PartitionTableItem> ParseQualcommRawProgramXml(string xmlContent)
        {
            var list = new List<PartitionTableItem>();
            try
            {
                var doc = XDocument.Parse(xmlContent);
                var programs = doc.Descendants("program");
                int index = 0;

                foreach (var p in programs)
                {
                    var label = p.Attribute("label")?.Value ?? $"part_{index}";
                    var filename = p.Attribute("filename")?.Value ?? string.Empty;
                    var startSector = long.TryParse(p.Attribute("start_sector")?.Value, out var ss) ? ss : 0;
                    var sectorCount = long.TryParse(p.Attribute("num_partition_sectors")?.Value, out var sc) ? sc : 0;
                    var sectorSize = long.TryParse(p.Attribute("SECTOR_SIZE_IN_BYTES")?.Value, out var sz) ? sz : 512;
                    if (sectorSize <= 0) sectorSize = 512;

                    var sizeBytes = sectorCount * sectorSize;
                    var sizeFormatted = sizeBytes >= 1048576 
                        ? $"{sizeBytes / (1024.0 * 1024.0):F1} MB" 
                        : $"{sizeBytes / 1024.0:F0} KB";

                    list.Add(new PartitionTableItem
                    {
                        Index = index++,
                        PartitionName = label,
                        FileName = filename,
                        StartSector = startSector,
                        SectorCount = sectorCount,
                        SizeBytes = sizeBytes,
                        SizeFormatted = sizeFormatted,
                        Flags = sectorSize == 4096 ? "UFS (4K)" : "eMMC (512B)",
                        RiskLevel = ClassifyPartitionRisk(label),
                        IsSelected = true
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"[XML Parse Error] {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Parse MediaTek Android_scatter.txt into structured partition items
        /// </summary>
        public List<PartitionTableItem> ParseMediaTekScatter(string scatterContent)
        {
            var list = new List<PartitionTableItem>();
            try
            {
                var blocks = Regex.Split(scatterContent, @"(?=- partition_index:|
partition_index:)");
                int index = 0;

                foreach (var block in blocks)
                {
                    if (!block.Contains("partition_name:")) continue;

                    var nameMatch = Regex.Match(block, @"partition_name:\s*([^
]+)");
                    var fileMatch = Regex.Match(block, @"file_name:\s*([^
]+)");
                    var sizeMatch = Regex.Match(block, @"partition_size:\s*0x([0-9a-fA-F]+)");
                    var offsetMatch = Regex.Match(block, @"linear_start_addr:\s*0x([0-9a-fA-F]+)");

                    if (!nameMatch.Success) continue;

                    var partName = nameMatch.Groups[1].Value.Trim();
                    var fileName = fileMatch.Success ? fileMatch.Groups[1].Value.Trim() : "NONE";
                    if (fileName.Equals("NONE", StringComparison.OrdinalIgnoreCase)) fileName = string.Empty;

                    long sizeBytes = 0;
                    if (sizeMatch.Success)
                    {
                        sizeBytes = Convert.ToInt64(sizeMatch.Groups[1].Value, 16);
                    }

                    long startAddr = 0;
                    if (offsetMatch.Success)
                    {
                        startAddr = Convert.ToInt64(offsetMatch.Groups[1].Value, 16);
                    }

                    var sizeFormatted = sizeBytes >= 1048576 
                        ? $"{sizeBytes / (1024.0 * 1024.0):F1} MB" 
                        : $"{sizeBytes / 1024.0:F0} KB";

                    list.Add(new PartitionTableItem
                    {
                        Index = index++,
                        PartitionName = partName,
                        FileName = fileName,
                        StartSector = startAddr / 512,
                        SectorCount = sizeBytes / 512,
                        SizeBytes = sizeBytes,
                        SizeFormatted = sizeFormatted,
                        Flags = "MediaTek BROM",
                        RiskLevel = ClassifyPartitionRisk(partName),
                        IsSelected = !string.IsNullOrEmpty(fileName)
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"[Scatter Parse Error] {ex.Message}");
            }
            return list;
        }

        // ==================== REAL FASTBOOT-BACKED PARTITION OPERATIONS ====================
        // Semua metode di bawah ini 100% disambungkan ke FastbootEngine.
        // Tidak ada dummy byte, tidak ada simulasi. Output terminal tampil langsung ke konsol.

        private FastbootEngine? _fastbootEngine;

        /// <summary>
        /// Inject FastbootEngine dependency for real partition operations
        /// </summary>
        public void SetFastbootEngine(FastbootEngine engine)
        {
            _fastbootEngine = engine;
            _fastbootEngine.OnLog += msg => OnLog?.Invoke(msg);
        }

        /// <summary>
        /// Dump/backup satu partisi menggunakan fastboot fetch (Android 12+ / Pixel / Fastbootd).
        /// Jika fetch tidak didukung oleh bootloader, laporkan jujur — tidak ada fallback dummy.
        /// </summary>
        public async Task<bool> ReadPartitionAsync(string portName, string partitionName, string outputPath, CancellationToken ct = default)
        {
            if (_fastbootEngine == null)
            {
                Log($"[ERROR] FastbootEngine belum tersedia. Sambungkan HP dalam mode Fastboot terlebih dahulu.");
                return false;
            }

            var risk = ClassifyPartitionRisk(partitionName);
            if (risk == PartitionRiskLevel.CriticalRadio)
            {
                Log($"[PERINGATAN] Partisi '{partitionName}' adalah partisi radio/baseband kritis (NVRAM/EFS/Modem).");
                Log($"[PERINGATAN] Pastikan HP benar-benar terhubung dalam Fastboot/Fastbootd sebelum melanjutkan.");
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            OnProgress?.Invoke(10);
            Log($"[Partition Manager] Memulai dump partisi '{partitionName}' → {outputPath}");

            var ok = await _fastbootEngine.FetchPartitionAsync(partitionName, outputPath, portName);

            OnProgress?.Invoke(100);
            if (ok)
                Log($"[SUKSES] Dump partisi '{partitionName}' selesai. File tersimpan: {outputPath}");
            else
                Log($"[GAGAL] Dump partisi '{partitionName}' tidak berhasil. Bootloader ini mungkin tidak mendukung fastboot fetch. Gunakan MTK BROM atau EDL 9008 untuk dump partisi pada bootloader terkunci.");

            return ok;
        }

        /// <summary>
        /// Hapus satu partisi menggunakan fastboot erase (real — langsung ke chip melalui USB).
        /// PERBAIKAN Cacat P3-3: Partisi CriticalRadio (NVRAM, EFS, Modemst) di-HARD BLOCK.
        /// Peringatan log saja tidak cukup — teknisi yang tidak sengaja klik bisa
        /// menghapus IMEI secara permanen tanpa ada yang menghentikannya.
        /// Gunakan overload dengan parameter force:true untuk operasi yang disengaja.
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string portName, string partitionName, CancellationToken ct = default, bool forceCritical = false)
        {
            if (_fastbootEngine == null)
            {
                Log($"[ERROR] FastbootEngine belum tersedia. Sambungkan HP dalam mode Fastboot terlebih dahulu.");
                return false;
            }

            var risk = ClassifyPartitionRisk(partitionName);
            if (risk == PartitionRiskLevel.CriticalRadio && !forceCritical)
            {
                // PERBAIKAN Cacat P3-3: HARD BLOCK — bukan hanya log.
                // Tanpa forceCritical=true, operasi erase pada partisi kritis DITOLAK.
                Log($"[DITOLAK] Operasi erase pada partisi kritis '{partitionName}' DIBLOKIR oleh sistem keamanan.");
                Log($"[INFO]    Partisi '{partitionName}' termasuk kategori RADIO/BASEBAND kritis (NVRAM/EFS/Modem).");
                Log($"[INFO]    Menghapus partisi ini dapat menyebabkan IMEI hilang PERMANEN atau HP mati total.");
                Log($"[INFO]    Jika operasi ini benar-benar disengaja oleh teknisi senior, gunakan opsi 'Force Critical Erase' di UI.");
                return false;
            }

            if (risk == PartitionRiskLevel.CriticalRadio)
            {
                Log($"[PERINGATAN KRITIS ✓] Erase paksa pada '{partitionName}' dilanjutkan atas konfirmasi eksplisit teknisi.");
                Log($"[PERINGATAN KRITIS ✓] Pastikan backup partisi tersedia sebelum melanjutkan.");
            }

            OnProgress?.Invoke(20);
            var ok = await _fastbootEngine.ErasePartitionAsync(partitionName, portName);
            OnProgress?.Invoke(100);

            return ok;
        }

        /// <summary>
        /// Flash satu partisi dengan file image menggunakan fastboot flash (real — menulis langsung ke chip).
        /// </summary>
        public async Task<bool> WritePartitionAsync(string portName, string partitionName, string inputFilePath, CancellationToken ct = default)
        {
            if (_fastbootEngine == null)
            {
                Log($"[ERROR] FastbootEngine belum tersedia. Sambungkan HP dalam mode Fastboot terlebih dahulu.");
                return false;
            }

            if (!File.Exists(inputFilePath))
            {
                Log($"[ERROR] File image tidak ditemukan: {inputFilePath}");
                return false;
            }

            var risk = ClassifyPartitionRisk(partitionName);
            if (risk == PartitionRiskLevel.CriticalRadio)
            {
                Log($"[PERINGATAN] Menulis ke partisi '{partitionName}' adalah operasi berisiko tinggi. File yang salah dapat menyebabkan IMEI hilang.");
            }

            var fileInfo = new FileInfo(inputFilePath);
            Log($"[Partition Manager] Flash '{fileInfo.Name}' ({fileInfo.Length / 1024.0:F1} KB) → partisi '{partitionName}'");

            OnProgress?.Invoke(15);
            var ok = await _fastbootEngine.FlashPartitionAsync(partitionName, inputFilePath, portName);
            OnProgress?.Invoke(100);

            return ok;
        }

        /// <summary>
        /// Read partition helper overload — menggunakan Fastboot Fetch jika device dalam mode Fastboot
        /// </summary>
        public async Task<string?> ReadPartitionAsync(PartitionTableItem partition, string outputDirectory, DeviceConnectionProtocol protocol = DeviceConnectionProtocol.AutoDetect, string? deviceParam = null, CancellationToken ct = default)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var fileName = $"{partition.PartitionName}_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
            var fullPath = Path.Combine(outputDirectory, fileName);
            var ok = await ReadPartitionAsync(deviceParam ?? string.Empty, partition.PartitionName, fullPath, ct);
            return ok ? fullPath : string.Empty;
        }

        /// <summary>
        /// Write partition helper overload — fastboot flash langsung ke chip
        /// </summary>
        public async Task<bool> WritePartitionAsync(PartitionTableItem partition, string imagePath, DeviceConnectionProtocol protocol = DeviceConnectionProtocol.AutoDetect, string? deviceParam = null, CancellationToken ct = default)
        {
            return await WritePartitionAsync(deviceParam ?? string.Empty, partition.PartitionName, imagePath, ct);
        }

        /// <summary>
        /// Erase partition helper overload — fastboot erase langsung ke chip.
        /// forceCritical: set true hanya jika teknisi senior sudah konfirmasi secara eksplisit via UI dialog.
        /// </summary>
        public async Task<bool> ErasePartitionAsync(PartitionTableItem partition, DeviceConnectionProtocol protocol = DeviceConnectionProtocol.AutoDetect, string? deviceParam = null, CancellationToken ct = default, bool forceCritical = false)
        {
            return await ErasePartitionAsync(deviceParam ?? string.Empty, partition.PartitionName, ct, forceCritical);
        }

        /// <summary>
        /// Explicit IPartitionIOEngine interface implementation — delegates to safe version (forceCritical=false).
        /// CriticalRadio partitions will be hard-blocked via the main implementation.
        /// </summary>
        Task<bool> IPartitionIOEngine.ErasePartitionAsync(PartitionTableItem partition, DeviceConnectionProtocol protocol, string? deviceParam, CancellationToken cancellationToken)
        {
            return ErasePartitionAsync(partition, protocol, deviceParam, cancellationToken, forceCritical: false);
        }
    }
}
