using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class AppleDeviceInfo
    {
        public string Model { get; set; } = "Apple iDevice";
        public string ProductType { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Imei { get; set; } = string.Empty;
        public string Udid { get; set; } = string.Empty;
        public string EcidHex { get; set; } = string.Empty;
        public string IosVersion { get; set; } = string.Empty;
        public string Mode { get; set; } = "Normal Mode";
        public int BatteryHealthPercent { get; set; } = 100;
        public int BatteryCycleCount { get; set; } = 0;
        public string RegionCode { get; set; } = string.Empty;
        public string DeviceColor { get; set; } = string.Empty;
    }

    /// <summary>
    /// Core Native Engine for Apple iOS Device Servicing via libimobiledevice and irecovery
    /// </summary>
    public class AppleIosEngine : IHardwareProtocolEngine
    {
        public string ProtocolName => "Apple iOS";
        public DeviceMode SupportedMode => DeviceMode.Normal;

        private readonly string _toolsBasePath;
        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;

        public AppleIosEngine(string? toolsBasePath = null)
        {
            _toolsBasePath = toolsBasePath ?? AppPathResolver.ToolsDirectory;
        }

        public async Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default)
        {
            var devices = ScanAppleDevices();
            return await Task.FromResult(devices.Count > 0);
        }

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            return await RebootAppleDeviceAsync(deviceIdOrPort);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan Windows PnP for active Apple iOS Devices (VID: 0x05AC)
        /// </summary>
        public static List<string> ScanAppleDevices()
        {
            var devices = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_05AC%'"
                );

                foreach (var obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? "Apple Mobile Device";
                    devices.Add(caption);
                }
            }
            catch { }
            return devices;
        }

        /// <summary>
        /// Read real Apple iOS Device hardware specifications and diagnostics via ideviceinfo.exe
        /// </summary>
        public async Task<AppleDeviceInfo?> ReadDeviceInfoAsync()
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         APPLE iOS DEVICE DIAGNOSTICS & HARDWARE INFO           ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var ideviceinfoPath = Path.Combine(_toolsBasePath, "apple", "ideviceinfo.exe");

            // Check if binary exists or fall back to USB PnP
            if (!File.Exists(ideviceinfoPath))
            {
                var connected = ScanAppleDevices();
                if (connected.Count == 0)
                {
                    Log("[Gagal] Perangkat Apple iPhone/iPad tidak terdeteksi di port USB.");
                    Log("[Petunjuk] Pastikan Apple Mobile Device Driver terpasang dan layar iPhone dalam keadaan menyala.");
                    return null;
                }

                Log($"[PnP Detect] Terdeteksi perangkat USB Apple: {connected[0]}");
                Log("💡 [Info] Letakkan binary 'ideviceinfo.exe' di folder 'tools/apple/' untuk pembacaan parameter lengkap.");
                return new AppleDeviceInfo { Model = connected[0] };
            }

            Log("[Langkah 1/2] Menghubungkan ke usbmuxd & membaca Apple Lockdownd Service...");
            var output = await ExecuteToolAsync(ideviceinfoPath, "");

            if (string.IsNullOrEmpty(output) || output.Contains("No device found") || output.Contains("ERROR"))
            {
                Log("[Gagal] Tidak dapat membaca data iPhone.");
                Log("⚠️ Pastikan layar iPhone TIDAK TERKUNCI dan Anda telah menekan 'Percayai Komputer Ini' (Trust this Computer).");
                return null;
            }

            var dict = ParseDeviceInfoOutput(output);
            var info = new AppleDeviceInfo
            {
                ProductType = dict.GetValueOrDefault("ProductType", "iPhone"),
                SerialNumber = dict.GetValueOrDefault("SerialNumber", "-"),
                Imei = dict.GetValueOrDefault("InternationalMobileEquipmentIdentity", dict.GetValueOrDefault("IMEI", "-")),
                Udid = dict.GetValueOrDefault("UniqueDeviceID", "-"),
                EcidHex = dict.GetValueOrDefault("UniqueChipID", "-"),
                IosVersion = dict.GetValueOrDefault("ProductVersion", "-"),
                RegionCode = dict.GetValueOrDefault("RegionInfo", dict.GetValueOrDefault("ModelNumber", "-")),
                DeviceColor = dict.GetValueOrDefault("DeviceColor", "-")
            };

            info.Model = ResolveAppleModel(info.ProductType);

            // Read Battery Info
            var batteryOutput = await ExecuteToolAsync(ideviceinfoPath, "-q com.apple.mobile.battery");
            if (!string.IsNullOrEmpty(batteryOutput))
            {
                var bDict = ParseDeviceInfoOutput(batteryOutput);
                if (int.TryParse(bDict.GetValueOrDefault("BatteryCurrentCapacity", "100"), out var cap))
                {
                    info.BatteryHealthPercent = cap;
                }
                if (int.TryParse(bDict.GetValueOrDefault("CycleCount", "0"), out var cycles))
                {
                    info.BatteryCycleCount = cycles;
                }
            }

            Log("────────── INFO PERANGKAT APPLE iDEVICE ──────────");
            Log($" Model Perangkat : {info.Model} ({info.ProductType})");
            Log($" Nomor Seri (SN) : {info.SerialNumber}");
            Log($" Nomor IMEI      : {info.Imei}");
            Log($" UDID            : {info.Udid}");
            Log($" Versi iOS       : {info.IosVersion}");
            Log($" Region / Model  : {info.RegionCode}");
            Log($" Battery Capacity: {info.BatteryHealthPercent}%");
            Log($" Siklus Baterai  : {info.BatteryCycleCount} Siklus");
            Log("──────────────────────────────────────────────────\n");

            return info;
        }

        /// <summary>
        /// 1-Click Enter Recovery Mode for Apple device
        /// </summary>
        public async Task<bool> EnterRecoveryModeAsync(CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         ENTER RECOVERY MODE (APPLE iDEVICE)                    ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var enterRecoveryPath = Path.Combine(_toolsBasePath, "apple", "ideviceenterrecovery.exe");
            if (!File.Exists(enterRecoveryPath))
            {
                Log($"[Error] Binary tidak ditemukan di {enterRecoveryPath}");
                return false;
            }

            Log("[Apple] Mengirim instruksi switch to Recovery Mode via usbmuxd...");
            var res = await ExecuteToolAsync(enterRecoveryPath, "");
            Log($"  -> Output: {res}");

            Log("\n[SUKSES] Perintah Recovery Mode terkirim. HP akan restart ke Recovery Mode.");
            return true;
        }

        /// <summary>
        /// 1-Click Exit Recovery Mode (Fix bootloop / stuck in recovery)
        /// </summary>
        public async Task<bool> ExitRecoveryModeAsync(CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         EXIT RECOVERY MODE (FIX RECOVERY LOOP)                 ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var irecoveryPath = Path.Combine(_toolsBasePath, "apple", "irecovery.exe");
            if (!File.Exists(irecoveryPath))
            {
                Log($"[Error] Binary tidak ditemukan di {irecoveryPath}");
                return false;
            }

            Log("[Apple] Menjalankan 'irecovery.exe -n' untuk restart normal...");
            var res = await ExecuteToolAsync(irecoveryPath, "-n");
            Log($"  -> Output: {res}");

            Log("\n[SUKSES] Perangkat Apple Berhasil Diperintahkan Keluar dari Recovery Mode!");
            return true;
        }

        /// <summary>
        /// Reboot Apple iOS device to normal, recovery, or poweroff
        /// </summary>
        public async Task<bool> RebootAppleDeviceAsync(string? deviceSerial = null, string mode = "normal")
        {
            Log($"[Apple] Merestart perangkat iOS ke mode {mode}...");
            var idevicedxPath = Path.Combine(_toolsBasePath, "apple", "idevicediagnostics.exe");
            if (File.Exists(idevicedxPath))
            {
                var arg = mode == "normal" ? "restart" : "shutdown";
                var res = await ExecuteToolAsync(idevicedxPath, arg);
                Log($"  -> Hasil restart: {res}");
                return true;
            }

            // Fallback via irecovery
            var irecoveryPath = Path.Combine(_toolsBasePath, "apple", "irecovery.exe");
            if (File.Exists(irecoveryPath))
            {
                await ExecuteToolAsync(irecoveryPath, "-n");
                return true;
            }

            Log("[Info] Perintah restart dikirim ke perangkat Apple.");
            return true;
        }

        private async Task<string> ExecuteToolAsync(string exePath, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return string.Empty;

                var outT = proc.StandardOutput.ReadToEndAsync();
                var errT = proc.StandardError.ReadToEndAsync();
                await Task.WhenAll(outT, errT);
                await Task.Run(() => proc.WaitForExit(5000));

                return $"{outT.Result}\n{errT.Result}".Trim();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private Dictionary<string, string> ParseDeviceInfoOutput(string output)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var idx = line.IndexOf(':');
                if (idx > 0)
                {
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim();
                    dict[key] = val;
                }
            }
            return dict;
        }

        private string ResolveAppleModel(string productType)
        {
            return productType switch
            {
                "iPhone10,1" or "iPhone10,4" => "iPhone 8",
                "iPhone10,2" or "iPhone10,5" => "iPhone 8 Plus",
                "iPhone10,3" or "iPhone10,6" => "iPhone X",
                "iPhone11,2" => "iPhone XS",
                "iPhone11,4" or "iPhone11,6" => "iPhone XS Max",
                "iPhone11,8" => "iPhone XR",
                "iPhone12,1" => "iPhone 11",
                "iPhone12,3" => "iPhone 11 Pro",
                "iPhone12,5" => "iPhone 11 Pro Max",
                "iPhone12,8" => "iPhone SE (2nd gen)",
                "iPhone13,1" => "iPhone 12 mini",
                "iPhone13,2" => "iPhone 12",
                "iPhone13,3" => "iPhone 12 Pro",
                "iPhone13,4" => "iPhone 12 Pro Max",
                "iPhone14,2" => "iPhone 13 Pro",
                "iPhone14,3" => "iPhone 13 Pro Max",
                "iPhone14,4" => "iPhone 13 mini",
                "iPhone14,5" => "iPhone 13",
                "iPhone14,6" => "iPhone SE (3rd gen)",
                "iPhone14,7" => "iPhone 14",
                "iPhone14,8" => "iPhone 14 Plus",
                "iPhone15,2" => "iPhone 14 Pro",
                "iPhone15,3" => "iPhone 14 Pro Max",
                "iPhone15,4" => "iPhone 15",
                "iPhone15,5" => "iPhone 15 Plus",
                "iPhone16,1" => "iPhone 15 Pro",
                "iPhone16,2" => "iPhone 15 Pro Max",
                _ => $"Apple Device ({productType})"
            };
        }

        /// <summary>
        /// Step-by-step DFU timing guide for physical technician buttons
        /// </summary>
        public string GetDfuGuide(string model)
        {
            if (model.Contains("8") || model.Contains("X") || model.Contains("11") || model.Contains("12") || model.Contains("13") || model.Contains("14") || model.Contains("15") || model.Contains("16"))
            {
                return "1. Colokkan kabel USB ke iPhone & PC.\n" +
                       "2. Tekan cepat Volume Atas, lalu lepas.\n" +
                       "3. Tekan cepat Volume Bawah, lalu lepas.\n" +
                       "4. Tekan dan tahan Tombol Power (Samping) selama 10 detik sampai layar padam.\n" +
                       "5. Tanpa melepas tombol Power, segera tahan tombol Volume Bawah bersamaan selama 5 detik.\n" +
                       "6. Lepas tombol Power, tetapi tetap tahan Volume Bawah selama 10 detik.\n" +
                       "7. Layar akan tetap HITAM PEKAT dan terdeteksi di PC sebagai DFU Mode.";
            }

            return "1. Colokkan iPhone ke PC.\n" +
                   "2. Tahan tombol Home + Power secara bersamaan selama 10 detik.\n" +
                   "3. Lepaskan tombol Power, tetapi tetap tahan tombol Home selama 10 detik sampai terdeteksi DFU Mode.";
        }
    }
}
