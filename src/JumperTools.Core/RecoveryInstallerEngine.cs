using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class RecoveryItem
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Codename { get; set; } = string.Empty;
        public string RecoveryType { get; set; } = "TWRP"; // TWRP, OrangeFox, PBRP, SHRP
        public string Version { get; set; } = "v3.7+";
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileSizeFormatted { get; set; } = string.Empty;
    }

    /// <summary>
    /// Native Engine for 1-Click Custom Recovery (TWRP / OrangeFox / PBRP) scanning and installation
    /// </summary>
    public class RecoveryInstallerEngine : IRecoveryInstallerEngine
    {
        private readonly string _toolsBasePath;
        private readonly string _databaseBasePath;
        private readonly FastbootEngine _fastbootEngine;
        private readonly string _adbPath;

        public event Action<string>? OnLog;

        public RecoveryInstallerEngine(string? toolsBasePath = null, string? databaseBasePath = null)
        {
            _toolsBasePath = toolsBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            _databaseBasePath = databaseBasePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database");
            var platformTools = Path.Combine(_toolsBasePath, "platform-tools");
            _adbPath = Path.Combine(platformTools, "adb.exe");
            _fastbootEngine = new FastbootEngine(platformTools);
            _fastbootEngine.OnLog += (msg) => OnLog?.Invoke(msg);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Ensure device is in Fastboot mode; if in ADB normal mode, reboot to bootloader automatically
        /// </summary>
        public async Task<bool> EnsureFastbootModeAsync(string? deviceSerial = null, CancellationToken ct = default)
        {
            if (await _fastbootEngine.IsDeviceConnectedAsync(deviceSerial))
            {
                return true;
            }

            if (File.Exists(_adbPath))
            {
                Log("[ADB -> Fastboot] Perangkat belum dalam mode Fastboot. Mengirim perintah 'adb reboot bootloader'...");
                var prefix = string.IsNullOrEmpty(deviceSerial) ? "" : $"-s {deviceSerial} ";
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _adbPath,
                        Arguments = $"{prefix}reboot bootloader",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    Log($"[ADB Warning] {ex.Message}");
                }

                Log("[Fastboot] Menunggu HP masuk ke mode Fastboot (maksimal 15 detik)...");
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(1000, ct);
                    if (await _fastbootEngine.IsDeviceConnectedAsync(deviceSerial))
                    {
                        Log("[Fastboot] HP terdeteksi dalam mode Fastboot! Melanjutkan proses...");
                        return true;
                    }
                }
            }

            Log("[Peringatan] HP belum terdeteksi di Fastboot. Pastikan kabel USB terhubung dan HP dalam mode Fastboot.");
            return false;
        }

        /// <summary>
        /// Scan database/recovery/ recursively for all custom recovery images
        /// </summary>
        public List<RecoveryItem> ScanAvailableRecoveries()
        {
            var list = new List<RecoveryItem>();
            var recoveryDir = Path.Combine(_databaseBasePath, "recovery");
            if (!Directory.Exists(recoveryDir))
            {
                try { Directory.CreateDirectory(recoveryDir); } catch { }
                return list;
            }

            try
            {
                var files = Directory.EnumerateFiles(recoveryDir, "*.img", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var relPath = Path.GetRelativePath(recoveryDir, file);
                    var parts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    var brand = parts.Length > 1 ? parts[0] : "Universal";
                    var model = parts.Length > 2 ? parts[1] : Path.GetFileNameWithoutExtension(file);

                    var nameLower = fileInfo.Name.ToLower();
                    var type = "TWRP";
                    if (nameLower.Contains("orangefox") || nameLower.Contains("ofox")) type = "OrangeFox";
                    else if (nameLower.Contains("pbrp") || nameLower.Contains("pitchblack")) type = "PBRP";
                    else if (nameLower.Contains("shrp")) type = "SHRP";

                    list.Add(new RecoveryItem
                    {
                        Brand = brand,
                        Model = model,
                        Codename = Path.GetFileNameWithoutExtension(file),
                        RecoveryType = type,
                        FileName = fileInfo.Name,
                        FilePath = file,
                        FileSizeFormatted = $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB"
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"[Recovery Scan Error] {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// 1-Click Flash Custom Recovery via Fastboot with optional AVB verification patch
        /// </summary>
        public async Task<bool> InstallRecoveryViaFastbootAsync(
            string recoveryImagePath, 
            string targetPartition = "auto",
            bool disableAvbVerity = true, 
            string? deviceSerial = null, 
            CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         1-CLICK CUSTOM RECOVERY INSTALLER (FASTBOOT)           ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!File.Exists(recoveryImagePath))
            {
                Log($"[Error] File image recovery tidak ditemukan: {recoveryImagePath}");
                return false;
            }

            var fileInfo = new FileInfo(recoveryImagePath);
            Log($"[Target] File Recovery   : {fileInfo.Name} ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)");
            Log($"[Target] Partisi Dipilih : {targetPartition.ToUpperInvariant()}");
            Log($"[Opsi]   Patch vbmeta    : {(disableAvbVerity ? "AKTIF (Disable Verity / AVB)" : "NON-AKTIF")}");

            // Ensure Fastboot Mode
            await EnsureFastbootModeAsync(deviceSerial, cancellationToken);

            // Step 1: Flash recovery
            bool okRecovery = false;
            if (string.Equals(targetPartition, "auto", StringComparison.OrdinalIgnoreCase) ||
                targetPartition.Contains("Auto", StringComparison.OrdinalIgnoreCase))
            {
                Log("\n[Langkah 1/3] Menulis image ke partisi [recovery]...");
                okRecovery = await _fastbootEngine.FlashPartitionAsync("recovery", recoveryImagePath, deviceSerial);
                if (!okRecovery)
                {
                    Log("  -> Mencoba fallback flashing ke [boot] (khusus arsitektur Virtual A/B)...");
                    okRecovery = await _fastbootEngine.FlashPartitionAsync("boot", recoveryImagePath, deviceSerial);
                }
                if (!okRecovery)
                {
                    Log("  -> Mencoba fallback flashing ke [init_boot] (Android 13+ GKI)...");
                    okRecovery = await _fastbootEngine.FlashPartitionAsync("init_boot", recoveryImagePath, deviceSerial);
                }
            }
            else
            {
                Log($"\n[Langkah 1/3] Menulis image langsung ke partisi [{targetPartition}]...");
                okRecovery = await _fastbootEngine.FlashPartitionAsync(targetPartition, recoveryImagePath, deviceSerial);
            }

            if (!okRecovery)
            {
                Log("\n[Gagal] Flashing recovery ditolak bootloader. Pastikan Bootloader (UBL) sudah UNLOCKED.");
                return false;
            }

            // Step 2: Disable AVB if requested and vbmeta is present
            if (disableAvbVerity)
            {
                Log("\n[Langkah 2/3] Memeriksa partisi vbmeta untuk menonaktifkan Android Verified Boot (AVB)...");
                var blankVbmeta = Path.Combine(_toolsBasePath, "platform-tools", "vbmeta_empty.img");
                if (File.Exists(blankVbmeta))
                {
                    Log("  -> Flashing empty vbmeta dengan flag disable-verity & disable-verification...");
                    await _fastbootEngine.FlashVbmetaDisabledAsync(blankVbmeta, deviceSerial);
                }
                else
                {
                    Log("  -> vbmeta_empty.img tidak ditemukan, melewati patch vbmeta.");
                }
            }

            // Step 3: Fastboot Reboot to Recovery
            Log("\n[Langkah 3/3] Merestart perangkat langsung ke Recovery Mode...");
            await _fastbootEngine.RebootAsync(FastbootRebootTarget.Recovery, deviceSerial);

            Log("\n════════════════════════════════════════════════════════════════");
            Log(" [SUKSES] Custom Recovery Berhasil Dipasang!");
            Log(" HP akan langsung masuk ke menu TWRP / OrangeFox.");
            Log("════════════════════════════════════════════════════════════════\n");

            return true;
        }

        /// <summary>
        /// Temporary boot recovery without flashing (fastboot boot <recovery.img>)
        /// </summary>
        public async Task<bool> TemporaryBootRecoveryAsync(string recoveryImagePath, string? deviceSerial = null, CancellationToken ct = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         TEMPORARY BOOT CUSTOM RECOVERY (RAM ONLY)              ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!File.Exists(recoveryImagePath))
            {
                Log($"[Error] File image recovery tidak ditemukan: {recoveryImagePath}");
                return false;
            }

            // Ensure Fastboot Mode
            await EnsureFastbootModeAsync(deviceSerial, ct);

            Log($"[Fastboot Boot] Mencoba booting sementara ke: {Path.GetFileName(recoveryImagePath)}...");
            var ok = await _fastbootEngine.BootImageAsync(recoveryImagePath, deviceSerial);
            if (ok)
            {
                Log("\n[SUKSES] Perintah Boot RAM berhasil dikirim. Perangkat sedang memuat Recovery...");
            }
            else
            {
                Log("\n[Info] Jika booting RAM ditolak, bootloader perangkat mungkin tidak mendukung 'fastboot boot'. Gunakan opsi 'Flash Recovery (Permanen)'.");
            }
            return ok;
        }
    }
}
