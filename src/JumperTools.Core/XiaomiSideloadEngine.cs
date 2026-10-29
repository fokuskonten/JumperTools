using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class XiaomiSideloadInfo
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string Codename { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string MiuiVersion { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public string LockState { get; set; } = "Locked";
        public string AntiRollback { get; set; } = "1";
    }

    /// <summary>
    /// Native Engine for Xiaomi / Redmi / Poco Recovery Sideload Mode (Mi Assistant)
    /// </summary>
    public class XiaomiSideloadEngine : IHardwareProtocolEngine
    {
        public string ProtocolName => "Xiaomi Sideload / Mi Assistant";
        public DeviceMode SupportedMode => DeviceMode.Recovery;

        private readonly string _adbPath;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;

        public XiaomiSideloadEngine(string platformToolsPath)
        {
            _adbPath = Path.Combine(platformToolsPath, "adb.exe");
        }

        public async Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default)
        {
            var serial = await GetSideloadDeviceSerialAsync();
            if (!string.IsNullOrEmpty(deviceIdOrPort))
                return string.Equals(serial, deviceIdOrPort, StringComparison.OrdinalIgnoreCase);
            return !string.IsNullOrEmpty(serial);
        }

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            var target = targetMode.ToLowerInvariant() switch
            {
                "bootloader" => "reboot bootloader",
                "recovery" => "reboot recovery",
                "edl" => "reboot edl",
                _ => "reboot"
            };
            var res = await ExecuteAdbCommandAsync(deviceIdOrPort ?? "", target);
            return !res.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Read detailed device information in Xiaomi Sideload / Mi Assistant mode
        /// </summary>
        public async Task<XiaomiSideloadInfo?> ReadDeviceInfoAsync(string? deviceSerial = null)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         XIAOMI SIDELOAD / MI ASSISTANT DEVICE INFO             ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            return await Task.Run(async () =>
            {
                var serial = string.IsNullOrEmpty(deviceSerial) ? await GetSideloadDeviceSerialAsync() : deviceSerial;
                if (string.IsNullOrEmpty(serial))
                {
                    Log("[Gagal] Perangkat dalam mode Sideload (Mi Assistant) tidak ditemukan.");
                    Log("[Petunjuk] Masuk ke Main Menu Recovery Xiaomi -> Pilih 'Connect with MIAssistant' -> Colok kabel USB.");
                    return null;
                }

                Log($"[Xiaomi Sideload] Terhubung ke perangkat: {serial}");

                var info = new XiaomiSideloadInfo { SerialNumber = serial };

                // Execute adb getprop or sideload variable query
                var rawVars = await ExecuteAdbCommandAsync(serial, "getprop");
                if (string.IsNullOrEmpty(rawVars) || rawVars.Contains("error"))
                {
                    // Fallback to getvar / devices
                    rawVars = await ExecuteAdbCommandAsync(serial, "devices -l");
                }

                // Parse Codename & Build
                var matchCodename = Regex.Match(rawVars, @"ro\.product\.name\]:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                info.Codename = matchCodename.Success ? matchCodename.Groups[1].Value : "Xiaomi / Redmi Device";

                var matchModel = Regex.Match(rawVars, @"ro\.product\.model\]:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                info.Model = matchModel.Success ? matchModel.Groups[1].Value : info.Codename;

                var matchMiui = Regex.Match(rawVars, @"ro\.build\.version\.incremental\]:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                info.MiuiVersion = matchMiui.Success ? matchMiui.Groups[1].Value : "MIUI / HyperOS Recovery";

                var matchAndroid = Regex.Match(rawVars, @"ro\.build\.version\.release\]:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
                info.AndroidVersion = matchAndroid.Success ? matchAndroid.Groups[1].Value : "Android 10 - 14";

                Log("────────── INFO PERANGKAT XIAOMI RECOVERY ──────────");
                Log($" Serial Number  : {info.SerialNumber}");
                Log($" Model / Nama   : {info.Model} ({info.Codename})");
                Log($" Versi ROM / OS : {info.MiuiVersion}");
                Log($" Android OS     : {info.AndroidVersion}");
                Log($" Status Lock    : {info.LockState} (Mi Assistant Active)");
                Log("────────────────────────────────────────────────────");

                return info;
            });
        }

        /// <summary>
        /// Format / Wipe Userdata in Xiaomi Sideload mode (Safe Factory Reset without unlocked bootloader)
        /// </summary>
        public async Task<bool> WipeUserdataAsync(string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         XIAOMI SIDELOAD FORMAT / WIPE USERDATA ENGINE          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var serial = string.IsNullOrEmpty(deviceSerial) ? await GetSideloadDeviceSerialAsync() : deviceSerial;
            if (string.IsNullOrEmpty(serial))
            {
                Log("[Gagal] Perangkat Xiaomi Sideload tidak terdeteksi.");
                OnUserActionRequired?.Invoke("Masuk ke Recovery Xiaomi -> Pilih 'Connect with MIAssistant' -> Colok kabel USB.");
                return false;
            }

            Log($"\n[1/3] Menghubungkan ke Mi Assistant Engine pada [{serial}]...");
            var getProp = await ExecuteAdbCommandAsync(serial, "getprop");

            Log("[2/3] Mengirim instruksi format partisi userdata...");
            var outWipe = await ExecuteAdbCommandAsync(serial, "shell recovery --wipe_data");
            if (string.IsNullOrEmpty(outWipe) || outWipe.Contains("error"))
            {
                // Try alternate sideload format command
                outWipe = await ExecuteAdbCommandAsync(serial, "raw-command format userdata");
            }
            Log($"  -> Respon Sideload: {outWipe.Replace('\r', ' ').Replace('\n', ' ').Trim()}");

            Log("\n[3/3] Memverifikasi status perangkat...");
            var isSuccess = EvaluateWipeResponse(outWipe);

            if (isSuccess)
            {
                Log("\n════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Perintah Format Userdata Xiaomi Selesai Dikirim!");
                Log(" Perangkat akan melakukan factory reset otomatis.");
                Log("════════════════════════════════════════════════════════════════\n");
            }
            else
            {
                Log("\n[Peringatan] Mi Assistant menolak perintah format langsung.");
                Log("Pastikan ROM pada perangkat mendukung Mi Assistant ADB format atau gunakan mode Fastboot/BROM.");
            }

            return isSuccess;
        }

        /// <summary>
        /// Reset FRP in Xiaomi Sideload Mode with HyperOS diagnostics and verified bypass guidance
        /// </summary>
        public async Task<bool> BypassFrpAsync(string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         XIAOMI SIDELOAD FRP & HYPEROS DIAGNOSTIC ENGINE        ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var serial = string.IsNullOrEmpty(deviceSerial) ? await GetSideloadDeviceSerialAsync() : deviceSerial;
            if (string.IsNullOrEmpty(serial))
            {
                Log("[Gagal] Perangkat Xiaomi Sideload tidak terdeteksi.");
                Log("[Petunjuk] Masuk ke Recovery Xiaomi -> Pilih 'Connect with MIAssistant' -> Colok USB.");
                return false;
            }

            Log($"[1/3] Menghubungkan ke Mi Assistant Engine pada [{serial}]...");
            var rawVars = await ExecuteAdbCommandAsync(serial, "getprop");
            var matchCodename = Regex.Match(rawVars, @"ro\.product\.name\]:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
            var matchMiui = Regex.Match(rawVars, @"ro\.build\.version\.incremental\]:\s*\[(.*?)\]", RegexOptions.IgnoreCase);
            var codename = matchCodename.Success ? matchCodename.Groups[1].Value : "Xiaomi Device";
            var osVersion = matchMiui.Success ? matchMiui.Groups[1].Value : "MIUI / HyperOS";

            Log($"  -> Teridentifikasi: {codename} (OS: {osVersion})");

            Log("[2/3] Menguji respon perintah wipe recovery bawaan...");
            var outWipe = await ExecuteAdbCommandAsync(serial, "shell recovery --wipe_data");
            
            if (!string.IsNullOrEmpty(outWipe) && (outWipe.Contains("OKAY", StringComparison.OrdinalIgnoreCase) || outWipe.Contains("success", StringComparison.OrdinalIgnoreCase)))
            {
                Log("════════════════════════════════════════════════════════════════");
                Log(" [SUKSES] Perintah Wipe FRP / Userdata Berhasil Dieksekusi!");
                Log("════════════════════════════════════════════════════════════════");
                return true;
            }

            // On official locked HyperOS / MIUI stock recovery, direct persist erase is rejected by security policy
            Log("\n[3/3] [DIAGNOSIS KEAMANAN HYPEROS / MIUI]");
            Log("────────────────────────────────────────────────────────────────");
            Log(" Stock Mi-Recovery mengunci izin modifikasi partisi secara offline.");
            Log(" Berikut 2 Rute Terverifikasi untuk Menyelesaikan Unit Ini:");
            Log("────────────────────────────────────────────────────────────────");
            Log(" 👉 JALUR 1 (GRATIS RP 0 - SENAM JARI HYPEROS 2025/2026):");
            Log("    1. Di layar 'Hi / Setel Telepon', tekan tombol Vol Up + Vol Down.");
            Log("    2. Aktifkan TalkBack -> Gambar huruf 'L' terbalik di layar.");
            Log("    3. Ucapkan perintah suara: 'Open Google Assistant' -> 'Open YouTube'.");
            Log("    4. Masuk ke Settings/Chrome via YouTube Profile -> Buka Pengaturan.");
            Log("    5. Masukkan Pola/PIN baru atau akun Google baru -> Restart HP.");
            Log("────────────────────────────────────────────────────────────────");
            Log(" 👉 JALUR 2 (HARDWARE TEST POINT / EDL 9008):");
            Log("    • Buka casing belakang -> Hubungkan titik Test Point (GND + CLK).");
            Log("    • Colok USB hingga terdeteksi 'Qualcomm HS-USB QDLoader 9008' atau 'MTK BROM'.");
            Log("    • Eksekusi tombol 'Bypass FRP Firehose' di tab Qualcomm/MediaTek.");
            Log("────────────────────────────────────────────────────────────────");

            return false;
        }

        /// <summary>
        /// Flash Official Recovery OTA Package (.zip) in Sideload Mode
        /// </summary>
        public async Task<bool> FlashOtaZipAsync(string otaZipPath, string? deviceSerial = null, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         XIAOMI SIDELOAD OFFICIAL OTA / RECOVERY FLASHER        ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            if (!File.Exists(otaZipPath))
            {
                Log($"[Error] File OTA zip tidak ditemukan: {otaZipPath}");
                return false;
            }

            var serial = string.IsNullOrEmpty(deviceSerial) ? await GetSideloadDeviceSerialAsync() : deviceSerial;
            if (string.IsNullOrEmpty(serial))
            {
                Log("[Gagal] Perangkat Xiaomi Sideload tidak terdeteksi.");
                return false;
            }

            Log($"[1/2] Mengirim paket ROM {Path.GetFileName(otaZipPath)} via adb sideload ke [{serial}] (Mohon tunggu beberapa menit)...");
            var outSideload = await ExecuteAdbCommandAsync(serial, $"sideload \"{otaZipPath}\"", 900000); // 15 menit timeout
            Log($"  -> Respon Sideload: {outSideload}");

            var isSuccess = !outSideload.Contains("error", StringComparison.OrdinalIgnoreCase) && 
                            !outSideload.Contains("failed", StringComparison.OrdinalIgnoreCase);

            if (isSuccess)
            {
                Log("\n[SUKSES] Pengiriman Paket Sideload Selesai!");
            }
            else
            {
                Log($"\n[Gagal] Sideload gagal atau ditolak perangkat.");
            }
            return isSuccess;
        }

        /// <summary>
        /// Reboot Xiaomi device from Sideload mode to normal system, fastboot, or recovery
        /// </summary>
        public async Task<bool> RebootAsync(string? deviceSerial, string target = "system")
        {
            var serial = string.IsNullOrEmpty(deviceSerial) ? await GetSideloadDeviceSerialAsync() : deviceSerial;
            if (string.IsNullOrEmpty(serial)) return false;

            Log($"[Xiaomi] Merestart perangkat ke mode [{target}]...");
            var arg = target == "system" ? "reboot" : $"reboot {target}";
            await ExecuteAdbCommandAsync(serial, arg);
            return true;
        }

        private async Task<string?> GetSideloadDeviceSerialAsync()
        {
            try
            {
                var output = await ExecuteAdbCommandAsync("", "devices");
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("sideload", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0) return parts[0];
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Evaluate ADB sideload recovery output to reliably determine if format/wipe succeeded without false positives
        /// </summary>
        public static bool EvaluateWipeResponse(string outWipe)
        {
            if (string.IsNullOrWhiteSpace(outWipe)) return false;

            var upper = outWipe.Trim();
            if (upper.Contains("OKAY", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("success", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("done", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Reject explicit error signatures
            if (upper.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("closed", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("not allowed", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                upper.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return false;
        }

        private async Task<string> ExecuteAdbCommandAsync(string serial, string arguments, int timeoutMs = 10000)
        {
            if (!File.Exists(_adbPath)) return "adb not found";

            try
            {
                var formattedArgs = string.IsNullOrEmpty(serial) ? arguments : $"-s {serial} {arguments}";
                var psi = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = formattedArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return string.Empty;

                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outTask, errTask);
                await Task.Run(() => process.WaitForExit(timeoutMs));

                return $"{outTask.Result}\n{errTask.Result}".Trim();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
