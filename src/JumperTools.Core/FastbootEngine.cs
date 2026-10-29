using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class FastbootDeviceInfo
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public bool? IsUnlocked { get; set; }
        public bool? IsSecure { get; set; }
        public string BootloaderVersion { get; set; } = string.Empty;
        public string BasebandVersion { get; set; } = string.Empty;
        public string CurrentSlot { get; set; } = string.Empty;
        public string MaxDownloadSize { get; set; } = string.Empty;
        public string HwRevision { get; set; } = string.Empty;
        public Dictionary<string, string> RawVariables { get; set; } = new();
    }

    public class AntiRollbackResult
    {
        public bool IsSupported { get; set; }
        public int AntiIndex { get; set; } = -1;
        public string RawOutput { get; set; } = string.Empty;
        public string Assessment { get; set; } = string.Empty;
        public bool IsDangerousToDowngrade { get; set; }
    }

    public enum FastbootRebootTarget
    {
        System,
        Bootloader,
        Fastbootd,
        Recovery,
        EDL
    }

    /// <summary>
    /// Native Core Engine for Fastboot protocol operations and variable parsing
    /// </summary>
    public class FastbootEngine : IHardwareProtocolEngine
    {
        private readonly string _fastbootPath;

        public string ProtocolName => "Fastboot";
        public DeviceMode SupportedMode => DeviceMode.Fastboot;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;

        public FastbootEngine(string platformToolsPath)
        {
            _fastbootPath = Path.Combine(platformToolsPath, "fastboot.exe");
        }

        public Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default) => IsDeviceConnectedAsync(deviceIdOrPort);

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            var target = targetMode.ToLowerInvariant() switch
            {
                "bootloader" => FastbootRebootTarget.Bootloader,
                "fastbootd" => FastbootRebootTarget.Fastbootd,
                "recovery" => FastbootRebootTarget.Recovery,
                "edl" => FastbootRebootTarget.EDL,
                _ => FastbootRebootTarget.System
            };
            return await RebootAsync(target, deviceIdOrPort);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Check if a device is connected in fastboot mode
        /// </summary>
        public async Task<bool> IsDeviceConnectedAsync(string? deviceSerial = null)
        {
            if (!File.Exists(_fastbootPath)) return false;
            try
            {
                var outDevices = await ExecuteFastbootRawAsync("devices");
                if (string.IsNullOrWhiteSpace(outDevices)) return false;
                if (string.IsNullOrEmpty(deviceSerial)) return outDevices.Contains("fastboot");
                return outDevices.Contains(deviceSerial);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Read and parse all fastboot variables (fastboot getvar all)
        /// </summary>
        public async Task<FastbootDeviceInfo> ReadAllVariablesAsync(string? deviceSerial = null)
        {
            var info = new FastbootDeviceInfo();
            if (!File.Exists(_fastbootPath))
            {
                Log($"[Error] fastboot.exe tidak ditemukan di: {_fastbootPath}");
                return info;
            }

            var args = string.IsNullOrEmpty(deviceSerial) ? "getvar all" : $"-s {deviceSerial} getvar all";
            Log($"[Fastboot] Menjalankan: fastboot {args}");

            var rawOutput = await ExecuteFastbootRawAsync(args);
            info = ParseVariables(rawOutput, deviceSerial);

            Log("────────── INFO PERANGKAT FASTBOOT ──────────");
            Log($" Product / Codename : {(string.IsNullOrEmpty(info.Product) ? "Unknown" : info.Product)}");
            Log($" Serial Number      : {(string.IsNullOrEmpty(info.SerialNumber) ? (deviceSerial ?? "Unknown") : info.SerialNumber)}");
            Log($" Bootloader Lock    : {(info.IsUnlocked.HasValue ? (info.IsUnlocked.Value ? "UNLOCKED (Terbuka)" : "LOCKED (Terkunci)") : "Unknown")}");
            Log($" Secure State       : {(info.IsSecure.HasValue ? (info.IsSecure.Value ? "YES" : "NO") : "Unknown")}");
            Log($" Bootloader Version : {(string.IsNullOrEmpty(info.BootloaderVersion) ? "Unknown" : info.BootloaderVersion)}");
            Log($" Baseband Version   : {(string.IsNullOrEmpty(info.BasebandVersion) ? "Unknown" : info.BasebandVersion)}");
            Log($" Active Slot        : {(string.IsNullOrEmpty(info.CurrentSlot) ? "Single / N/A" : info.CurrentSlot)}");
            Log("─────────────────────────────────────────────");

            return info;
        }

        /// <summary>
        /// Parse fastboot getvar raw output lines into FastbootDeviceInfo structure
        /// </summary>
        public static FastbootDeviceInfo ParseVariables(string rawOutput, string? fallbackSerial = null)
        {
            var info = new FastbootDeviceInfo();
            if (string.IsNullOrWhiteSpace(rawOutput)) return info;

            var lines = rawOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("(bootloader)", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase))
                {
                    // Pattern: (bootloader) key: value  OR  getvar: key: value
                    var match = Regex.Match(trimmed, @"^(?:\(bootloader\)|getvar:)\s*([^:]+):\s*(.*)$", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var key = match.Groups[1].Value.Trim();
                        var val = match.Groups[2].Value.Trim();
                        info.RawVariables[key] = val;

                        if (key.Equals("product", StringComparison.OrdinalIgnoreCase))
                            info.Product = val;
                        else if (key.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
                            info.IsUnlocked = val.Equals("yes", StringComparison.OrdinalIgnoreCase) || val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";
                        else if (key.Equals("secure", StringComparison.OrdinalIgnoreCase))
                            info.IsSecure = val.Equals("yes", StringComparison.OrdinalIgnoreCase) || val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";
                        else if (key.Equals("serialno", StringComparison.OrdinalIgnoreCase))
                            info.SerialNumber = val;
                        else if (key.Equals("version-bootloader", StringComparison.OrdinalIgnoreCase))
                            info.BootloaderVersion = val;
                        else if (key.Equals("version-baseband", StringComparison.OrdinalIgnoreCase))
                            info.BasebandVersion = val;
                        else if (key.Equals("current-slot", StringComparison.OrdinalIgnoreCase))
                            info.CurrentSlot = val;
                        else if (key.Equals("max-download-size", StringComparison.OrdinalIgnoreCase))
                            info.MaxDownloadSize = val;
                        else if (key.Equals("hw-revision", StringComparison.OrdinalIgnoreCase))
                            info.HwRevision = val;
                    }
                }
            }

            if (string.IsNullOrEmpty(info.SerialNumber) && !string.IsNullOrEmpty(fallbackSerial))
            {
                info.SerialNumber = fallbackSerial;
            }

            return info;
        }

        /// <summary>
        /// Erase FRP and persistent partitions
        /// </summary>
        public async Task<bool> EraseFrpAsync(string? deviceSerial = null)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║             FASTBOOT ERASE FRP & PERSISTENT ENGINE             ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var targetPartitions = new[] { "frp", "persistent", "config" };
            var anySuccess = false;

            foreach (var partition in targetPartitions)
            {
                var args = string.IsNullOrEmpty(deviceSerial) ? $"erase {partition}" : $"-s {deviceSerial} erase {partition}";
                Log($"[Fastboot] Menghapus partisi [{partition}]...");

                var output = await ExecuteFastbootRawAsync(args);
                var isOkay = output.Contains("OKAY");

                if (isOkay)
                {
                    Log($"  -> Partisi [{partition}] BERHASIL dihapus! (OKAY)");
                    anySuccess = true;
                }
                else
                {
                    var cleanError = output.Replace("\r", " ").Replace("\n", " ").Trim();
                    Log($"  -> Partisi [{partition}] dilewati / gagal: {cleanError}");
                }
            }

            if (anySuccess)
            {
                Log("\n[SUKSES] Penghapusan partisi FRP/Persistent selesai!");
                Log("Silakan restart perangkat dengan perintah Fastboot Reboot.");
            }
            else
            {
                Log("\n[Peringatan] Seluruh partisi ditolak oleh bootloader.");
                Log("Jika bootloader dalam status LOCKED, gunakan mode MTK BROM atau EDL 9008.");
            }

            return anySuccess;
        }

        /// <summary>
        /// Factory reset by wiping userdata and cache
        /// </summary>
        public async Task<bool> FactoryResetAsync(string? deviceSerial = null)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║             FASTBOOT FACTORY RESET (WIPE DATA)                 ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";

            Log("[1/2] Menghapus partisi userdata...");
            var outData = await ExecuteFastbootRawAsync($"{prefix}erase userdata");
            var okData = outData.Contains("OKAY");
            Log($"  -> Hasil erase userdata: {(okData ? "OKAY" : outData.Trim())}");

            Log("[2/2] Menghapus partisi cache...");
            var outCache = await ExecuteFastbootRawAsync($"{prefix}erase cache");
            var okCache = outCache.Contains("OKAY");
            Log($"  -> Hasil erase cache: {(okCache ? "OKAY" : outCache.Trim())}");

            return okData || okCache;
        }

        /// <summary>
        /// Attempt OEM Bootloader Unlock
        /// </summary>
        public async Task<bool> UnlockBootloaderAsync(string? deviceSerial = null)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║             FASTBOOT BOOTLOADER UNLOCK ENGINE                  ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            var unlockCommands = new[]
            {
                $"{prefix}flashing unlock",
                $"{prefix}oem unlock",
                $"{prefix}flashing unlock_critical"
            };

            foreach (var cmd in unlockCommands)
            {
                Log($"[Fastboot] Mencoba: fastboot {cmd}");
                var outUnlock = await ExecuteFastbootRawAsync(cmd);
                if (outUnlock.Contains("OKAY"))
                {
                    Log($"[SUKSES] Bootloader Unlock diizinkan! (OKAY)");
                    Log("Periksa layar HP jika ada konfirmasi tombol Volume untuk menyetujui.");
                    return true;
                }
                Log($"  -> Respon: {outUnlock.Trim()}");
            }

            Log("[Info] Jika perintah unlock ditolak, pastikan 'OEM Unlocking' sudah diaktifkan di Opsi Pengembang.");
            return false;
        }

        /// <summary>
        /// Attempt OEM Bootloader Lock
        /// </summary>
        public async Task<bool> LockBootloaderAsync(string? deviceSerial = null)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║             FASTBOOT BOOTLOADER LOCK ENGINE                    ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            var lockCommands = new[]
            {
                $"{prefix}flashing lock",
                $"{prefix}oem lock"
            };

            foreach (var cmd in lockCommands)
            {
                Log($"[Fastboot] Mencoba: fastboot {cmd}");
                var outLock = await ExecuteFastbootRawAsync(cmd);
                if (outLock.Contains("OKAY"))
                {
                    Log($"[SUKSES] Bootloader Lock diizinkan! (OKAY)");
                    return true;
                }
                Log($"  -> Respon: {outLock.Trim()}");
            }

            return false;
        }

        /// <summary>
        /// Set active slot for A/B partitioning (fastboot --set-active=a/b)
        /// </summary>
        public async Task<bool> SetActiveSlotAsync(string slot, string? deviceSerial = null)
        {
            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            Log($"[Fastboot] Mengubah slot aktif ke [{slot}]...");
            var outSlot = await ExecuteFastbootRawAsync($"{prefix}--set-active={slot}");
            if (!outSlot.Contains("OKAY"))
            {
                outSlot = await ExecuteFastbootRawAsync($"{prefix}set_active {slot}");
            }
            var ok = outSlot.Contains("OKAY");
            Log($"  -> Hasil set active slot: {(ok ? "OKAY" : outSlot.Trim())}");
            return ok;
        }

        /// <summary>
        /// Format/erase specific partition
        /// </summary>
        public async Task<bool> FormatPartitionAsync(string partitionName, string? deviceSerial = null)
        {
            return await ErasePartitionAsync(partitionName, deviceSerial);
        }

        /// <summary>
        /// Reboot fastboot device to target mode
        /// </summary>
        public async Task<bool> RebootAsync(FastbootRebootTarget target, string? deviceSerial = null)
        {
            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            string cmd;
            string targetName;

            switch (target)
            {
                case FastbootRebootTarget.Bootloader:
                    cmd = $"{prefix}reboot bootloader";
                    targetName = "Bootloader (Fastboot)";
                    break;
                case FastbootRebootTarget.Fastbootd:
                    cmd = $"{prefix}reboot fastboot";
                    targetName = "Fastbootd (Userspace)";
                    break;
                case FastbootRebootTarget.Recovery:
                    cmd = $"{prefix}reboot recovery";
                    targetName = "Recovery Mode";
                    break;
                case FastbootRebootTarget.EDL:
                    cmd = $"{prefix}oem edl";
                    targetName = "Qualcomm EDL (9008)";
                    break;
                case FastbootRebootTarget.System:
                default:
                    cmd = $"{prefix}reboot";
                    targetName = "Normal System";
                    break;
            }

            Log($"[Fastboot] Mengirim perintah restart ke [{targetName}]...");
            var outReboot = await ExecuteFastbootRawAsync(cmd);

            // Fallback for EDL if "oem edl" was rejected
            if (target == FastbootRebootTarget.EDL && !outReboot.Contains("OKAY"))
            {
                Log("  -> Mencoba fallback: fastboot reboot edl");
                outReboot = await ExecuteFastbootRawAsync($"{prefix}reboot edl");
            }

            Log($"  -> Respon: {outReboot.Trim()}");
            return outReboot.Contains("OKAY") || outReboot.Contains("Rebooting");
        }

        /// <summary>
        /// Read and check Anti-Rollback (ARB) index on Qualcomm / Xiaomi / Android Fastboot
        /// </summary>
        public async Task<AntiRollbackResult> GetAntiRollbackInfoAsync(string? deviceSerial = null)
        {
            var result = new AntiRollbackResult();
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║             FASTBOOT ANTI-ROLLBACK (ARB) CHECKER               ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            
            Log("[1/2] Mengecek variabel anti-rollback (fastboot getvar anti)...");
            var outAnti = await ExecuteFastbootRawAsync($"{prefix}getvar anti");
            result.RawOutput = outAnti;

            var matchAnti = Regex.Match(outAnti, @"(?:anti|rollback_index):\s*(\d+)", RegexOptions.IgnoreCase);
            if (matchAnti.Success && int.TryParse(matchAnti.Groups[1].Value, out var idx))
            {
                result.IsSupported = true;
                result.AntiIndex = idx;
            }
            else
            {
                // Fallback check rollback_index
                var outRollback = await ExecuteFastbootRawAsync($"{prefix}getvar rollback_index");
                var matchRollback = Regex.Match(outRollback, @"rollback_index:\s*(\d+)", RegexOptions.IgnoreCase);
                if (matchRollback.Success && int.TryParse(matchRollback.Groups[1].Value, out var rIdx))
                {
                    result.IsSupported = true;
                    result.AntiIndex = rIdx;
                    result.RawOutput += $"\n{outRollback}";
                }
            }

            Log("──────────────── STATUS ANTI-ROLLBACK (ARB) ────────────────");
            if (result.IsSupported)
            {
                Log($" Indeks ARB Aktif  : ARB Index = {result.AntiIndex}");
                if (result.AntiIndex >= 4)
                {
                    result.IsDangerousToDowngrade = true;
                    result.Assessment = $"Proteksi ARB Tinggi (Index {result.AntiIndex}). DILARANG KERAS downgrade ke versi firmware dengan ARB lebih rendah (Hard-Brick!).";
                    Log($" Status Proteksi   : ⚠️ PROTEKSI TINGGI (Index {result.AntiIndex})");
                    Log($" Rekomendasi       : Flash HANYA ROM dengan ARB >= {result.AntiIndex}!");
                }
                else
                {
                    result.IsDangerousToDowngrade = false;
                    result.Assessment = $"Proteksi Normal (Index {result.AntiIndex}). Aman untuk flashing ROM kompatibel.";
                    Log($" Status Proteksi   : NORMAL (Index {result.AntiIndex})");
                    Log(" Rekomendasi       : Flashing aman.");
                }
            }
            else
            {
                result.Assessment = "Perangkat tidak mengembalikan variabel ARB atau proteksi ARB dinonaktifkan di level bootloader.";
                Log(" Status Proteksi   : ARB N/A (Tidak Ada / Unlocked Device)");
                Log($" Respon Raw        : {result.RawOutput.Replace('\r', ' ').Replace('\n', ' ').Trim()}");
            }
            Log("────────────────────────────────────────────────────────────\n");

            return result;
        }

        /// <summary>
        /// Flash a partition with an image file via fastboot.
        /// PERBAIKAN Cacat P3-2: Timeout ditingkatkan ke 600 detik untuk flash.
        /// fastboot flash pada partisi super/userdata/system (4-8 GB) membutuhkan
        /// waktu 3-10 menit. Timeout 6 detik akan kill proses di tengah write → BRICK.
        /// </summary>
        public async Task<bool> FlashPartitionAsync(string partitionName, string imageFilePath, string? deviceSerial = null)
        {
            if (!File.Exists(imageFilePath))
            {
                Log($"[Error] File image tidak ditemukan: {imageFilePath}");
                return false;
            }

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            var fileInfo = new FileInfo(imageFilePath);
            var estimatedMinutes = fileInfo.Length / (1024.0 * 1024 * 30); // ~30MB/s USB 2.0 throughput
            var flashTimeoutMs = Math.Max(600_000, (int)(estimatedMinutes * 60 * 1000) + 60_000);
            Log($"[Fastboot] Menjalankan: fastboot {prefix}flash {partitionName} \"{imageFilePath}\" (timeout: {flashTimeoutMs / 1000}s)");

            var output = await ExecuteFastbootRawAsync($"{prefix}flash {partitionName} \"{imageFilePath}\"", flashTimeoutMs);
            var isSuccess = output.Contains("OKAY");

            if (isSuccess)
            {
                Log($"[SUKSES] Partisi [{partitionName}] berhasil di-flash! (OKAY)");
            }
            else
            {
                Log($"[GAGAL] Flashing partisi [{partitionName}] ditolak bootloader:");
                Log($"  -> Respon: {output.Trim()}");
            }

            return isSuccess;
        }

        /// <summary>
        /// Flash vbmeta partition with --disable-verity and --disable-verification flags to prevent bootloop on custom recovery
        /// </summary>
        public async Task<bool> FlashVbmetaDisabledAsync(string vbmetaImagePath, string? deviceSerial = null)
        {
            if (!File.Exists(vbmetaImagePath))
            {
                Log($"[Error] File vbmeta tidak ditemukan: {vbmetaImagePath}");
                return false;
            }

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            Log($"[Fastboot] Menjalankan: fastboot {prefix}--disable-verity --disable-verification flash vbmeta \"{vbmetaImagePath}\"");

            var output = await ExecuteFastbootRawAsync($"{prefix}--disable-verity --disable-verification flash vbmeta \"{vbmetaImagePath}\"");
            var isSuccess = output.Contains("OKAY");

            if (!isSuccess)
            {
                // Fallback standard flash
                output = await ExecuteFastbootRawAsync($"{prefix}flash vbmeta \"{vbmetaImagePath}\"");
                isSuccess = output.Contains("OKAY");
            }

            return isSuccess;
        }

        /// <summary>
        /// Temporary boot kernel/recovery image in RAM without flashing to storage (fastboot boot <image.img>)
        /// </summary>
        public async Task<bool> BootImageAsync(string imageFilePath, string? deviceSerial = null)
        {
            if (!File.Exists(imageFilePath))
            {
                Log($"[Error] File image tidak ditemukan: {imageFilePath}");
                return false;
            }

            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            Log($"[Fastboot] Menjalankan: fastboot {prefix}boot \"{imageFilePath}\"");

            var output = await ExecuteFastbootRawAsync($"{prefix}boot \"{imageFilePath}\"");
            var isSuccess = output.Contains("OKAY") || output.Contains("booting");

            if (isSuccess)
            {
                Log($"[SUKSES] Perangkat berhasil booting sementara ke: {Path.GetFileName(imageFilePath)} (OKAY)");
            }
            else
            {
                Log($"[GAGAL] Booting image ditolak bootloader:");
                Log($"  -> Respon: {output.Trim()}");
            }

            return isSuccess;
        }

        /// <summary>
        /// Erase a single partition via fastboot
        /// </summary>
        public async Task<bool> ErasePartitionAsync(string partitionName, string? deviceSerial = null)
        {
            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            Log($"[Fastboot] Menjalankan: fastboot {prefix}erase {partitionName}");

            var output = await ExecuteFastbootRawAsync($"{prefix}erase {partitionName}");
            var isSuccess = output.Contains("OKAY");

            if (isSuccess)
            {
                Log($"[SUKSES] Partisi [{partitionName}] berhasil dihapus (Erase OKAY)");
            }
            else
            {
                Log($"[GAGAL] Erase partisi [{partitionName}] ditolak:");
                Log($"  -> Respon: {output.Trim()}");
            }

            return isSuccess;
        }

        /// <summary>
        /// Attempt to dump/fetch a partition via fastboot fetch (supported on Fastbootd / newer bootloaders).
        /// PERBAIKAN Cacat P3-1: Validasi ukuran file > 0 (File.Exists() saja tidak cukup).
        /// PERBAIKAN Cacat P3-2: Timeout 300 detik untuk dump partisi besar.
        /// </summary>
        public async Task<bool> FetchPartitionAsync(string partitionName, string outputFilePath, string? deviceSerial = null)
        {
            var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
            Log($"[Fastboot] Menjalankan: fastboot {prefix}fetch {partitionName} \"{outputFilePath}\"");

            var output = await ExecuteFastbootRawAsync($"{prefix}fetch {partitionName} \"{outputFilePath}\"", 300_000);

            // PERBAIKAN Cacat P3-1: Cek file ada DAN ukuran > 0.
            // Fastboot fetch yang gagal di tengah jalan menghasilkan file 0-byte yang
            // dilaporkan sebagai sukses oleh File.Exists() semata.
            var fileValid = File.Exists(outputFilePath) && new FileInfo(outputFilePath).Length > 0;
            var isSuccess = output.Contains("OKAY") && fileValid;

            if (isSuccess)
            {
                var size = new FileInfo(outputFilePath).Length;
                Log($"[SUKSES] Partisi [{partitionName}] berhasil di-fetch ({size / 1024.0:F1} KB) → {outputFilePath}");
            }
            else
            {
                if (File.Exists(outputFilePath) && new FileInfo(outputFilePath).Length == 0)
                    Log($"[Peringatan] File hasil fetch [{partitionName}] berukuran 0-byte — dump tidak lengkap.");
                else
                    Log($"[Info] Fastboot Fetch partisi [{partitionName}] tidak didukung oleh bootloader ini.");
                Log($"  -> Respon: {output.Trim()}");
                Log("  -> Catatan: Untuk dump partisi pada bootloader terkunci, gunakan MTK BROM atau Qualcomm EDL 9008.");
            }

            return isSuccess;
        }

        /// <summary>
        /// Low-level process runner for fastboot CLI with live streaming and safe timeout
        /// </summary>
        private async Task<string> ExecuteFastbootRawAsync(string arguments, int timeoutMs = 6000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _fastbootPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var sb = new System.Text.StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        sb.AppendLine(e.Data);
                        Log($"  [Fastboot] {e.Data}");
                    }
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        sb.AppendLine(e.Data);
                        Log($"  [Fastboot] {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() => process.WaitForExit(timeoutMs));
                if (!completed)
                {
                    try { process.Kill(true); } catch { }
                    Log("  [Fastboot Timeout] Perangkat tidak merespons. Pastikan kabel terhubung dan driver Fastboot terpasang.");
                }
                else
                {
                    process.WaitForExit();
                }

                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                var err = $"Error: {ex.Message}";
                Log($"  [Fastboot Error] {err}");
                return err;
            }
        }
    }
}
