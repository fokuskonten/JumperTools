using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core.Interfaces;
using JumperTools.Models;

namespace JumperTools.Core
{
    public class SamsungPortInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
    }

    public class SamsungDeviceInfo
    {
        public string PortName { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Imei { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string SoftwareVersion { get; set; } = string.Empty;
        public string Csc { get; set; } = string.Empty;
        public string BuildNumber { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public Dictionary<string, string> RawProperties { get; set; } = new();
    }

    public enum SamsungRebootMode
    {
        Normal = 2,
        Download = 4
    }

    /// <summary>
    /// Core Native Engine for communicating with Samsung devices via MTP / Modem AT Commands
    /// </summary>
    public class SamsungAtEngine : IHardwareProtocolEngine
    {
        public string ProtocolName => "Samsung AT Modem";
        public DeviceMode SupportedMode => DeviceMode.Normal;

        public event Action<string>? OnLog;
        public event Action<string>? OnUserActionRequired;

        public Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default)
        {
            var ports = ScanSamsungPorts();
            if (!string.IsNullOrEmpty(deviceIdOrPort))
                return Task.FromResult(ports.Any(p => p.PortName.Equals(deviceIdOrPort, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(ports.Count > 0);
        }

        public async Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default)
        {
            var port = deviceIdOrPort;
            if (string.IsNullOrEmpty(port))
            {
                var ports = ScanSamsungPorts();
                if (ports.Count == 0) return false;
                port = ports[0].PortName;
            }

            var mode = targetMode.Equals("download", StringComparison.OrdinalIgnoreCase) 
                ? SamsungRebootMode.Download 
                : SamsungRebootMode.Normal;
            return await RebootAsync(port, mode);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan Windows PnP entities to find active Samsung Mobile USB Modem COM Ports
        /// </summary>
        public static List<SamsungPortInfo> ScanSamsungPorts()
        {
            var ports = new List<SamsungPortInfo>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%SAMSUNG%' AND (Caption LIKE '%Modem%' OR Caption LIKE '%Serial%' OR Caption LIKE '%COM%')"
                );

                foreach (var obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? string.Empty;
                    var pnpId = obj["PNPDeviceID"]?.ToString() ?? string.Empty;

                    var match = Regex.Match(caption, @"\((COM\d+)\)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var portName = match.Groups[1].Value.ToUpper();
                        if (!ports.Any(p => p.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase)))
                        {
                            ports.Add(new SamsungPortInfo
                            {
                                PortName = portName,
                                Description = caption,
                                HardwareId = pnpId
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback: check standard serial ports if WMI query fails
                try
                {
                    var standardPorts = SerialPort.GetPortNames();
                    foreach (var p in standardPorts)
                    {
                        if (!ports.Any(x => x.PortName.Equals(p, StringComparison.OrdinalIgnoreCase)))
                        {
                            ports.Add(new SamsungPortInfo
                            {
                                PortName = p,
                                Description = $"Serial Port ({p})"
                            });
                        }
                    }
                }
                catch { }
            }

            return ports;
        }

        /// <summary>
        /// Send a single AT command to a Samsung COM port and get response
        /// </summary>
        public async Task<string> SendAtCommandAsync(string portName, string command, int timeoutMs = 3000)
        {
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                SerialPort? port = null;

                try
                {
                    port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = timeoutMs,
                        WriteTimeout = timeoutMs,
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    port.Open();

                    // Discard any residual buffer
                    port.DiscardInBuffer();
                    port.DiscardOutBuffer();

                    var cmdBytes = Encoding.ASCII.GetBytes(command + "\r\n");
                    port.Write(cmdBytes, 0, cmdBytes.Length);

                    var startTime = DateTime.Now;
                    while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                    {
                        if (port.BytesToRead > 0)
                        {
                            var buffer = new byte[port.BytesToRead];
                            var bytesRead = port.Read(buffer, 0, buffer.Length);
                            sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                            var text = sb.ToString();
                            if (text.Contains("OK\r") || text.Contains("OK\n") || text.Contains("ERROR\r") || text.Contains("ERROR\n"))
                            {
                                break;
                            }
                        }
                        Thread.Sleep(50);
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[Error: {ex.Message}]");
                }
                finally
                {
                    if (port != null && port.IsOpen)
                    {
                        try { port.Close(); } catch { }
                    }
                }

                return sb.ToString();
            });
        }

        /// <summary>
        /// Read detailed Samsung Device Information using Modem AT commands
        /// </summary>
        public async Task<SamsungDeviceInfo?> ReadDeviceInfoAsync(string portName)
        {
            Log($"Menghubungkan ke port modem Samsung [{portName}]...");

            var handshake = await SendAtCommandAsync(portName, "AT", 2000);
            if (!handshake.Contains("OK"))
            {
                Log($"[Gagal] Modem pada port {portName} tidak merespons perintah AT.");
                return null;
            }

            var info = new SamsungDeviceInfo { PortName = portName };

            // 1. Read Device Connection Info (Model, IMEI, CSC, Build ID)
            var devInfo = await SendAtCommandAsync(portName, "AT+DEVCONINFO", 3000);
            if (!string.IsNullOrWhiteSpace(devInfo))
            {
                ParseDevConInfo(devInfo, info);
            }

            // 2. Read Software Version
            var swVer = await SendAtCommandAsync(portName, "AT+SWVER", 2000);
            if (!string.IsNullOrWhiteSpace(swVer) && swVer.Contains("+SWVER:"))
            {
                var match = Regex.Match(swVer, @"\+SWVER:\s*([^\r\n]+)");
                if (match.Success)
                {
                    info.SoftwareVersion = match.Groups[1].Value.Trim();
                }
            }

            // 3. Read Version Name
            if (string.IsNullOrWhiteSpace(info.SoftwareVersion))
            {
                var versName = await SendAtCommandAsync(portName, "AT+VERSNAME=3", 2000);
                if (!string.IsNullOrWhiteSpace(versName) && versName.Contains("+VERSNAME:"))
                {
                    var match = Regex.Match(versName, @"\+VERSNAME:\s*([^\r\n]+)");
                    if (match.Success)
                    {
                        info.SoftwareVersion = match.Groups[1].Value.Trim();
                    }
                }
            }

            Log("────────── INFO PERANGKAT SAMSUNG ──────────");
            Log($" Port COM        : {info.PortName}");
            Log($" Model           : {(string.IsNullOrEmpty(info.Model) ? "Unknown" : info.Model)}");
            Log($" IMEI            : {(string.IsNullOrEmpty(info.Imei) ? "Unknown" : info.Imei)}");
            Log($" Serial Number   : {(string.IsNullOrEmpty(info.SerialNumber) ? "Unknown" : info.SerialNumber)}");
            Log($" CSC / Region    : {(string.IsNullOrEmpty(info.Csc) ? "Unknown" : info.Csc)}");
            Log($" Firmware Ver    : {(string.IsNullOrEmpty(info.SoftwareVersion) ? "Unknown" : info.SoftwareVersion)}");
            Log($" Android OS      : {(string.IsNullOrEmpty(info.AndroidVersion) ? "Unknown" : info.AndroidVersion)}");
            Log("───────────────────────────────────────────");

            return info;
        }

        public static void ParseDevConInfo(string raw, SamsungDeviceInfo info)
        {
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Format 1: +DEVCONINFO: Model=SM-A515F, IMEI=358941092837194, SWVER=A515FXXU5FVD2
                if (trimmed.StartsWith("+DEVCONINFO:", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = trimmed.Substring("+DEVCONINFO:".Length);
                    var pairs = payload.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split(new[] { '=' }, 2);
                        if (kv.Length == 2)
                        {
                            var key = kv[0].Trim();
                            var val = kv[1].Trim();
                            info.RawProperties[key] = val;

                            if (key.Equals("Model", StringComparison.OrdinalIgnoreCase) || key.Equals("MN", StringComparison.OrdinalIgnoreCase))
                                info.Model = val;
                            else if (key.Equals("IMEI", StringComparison.OrdinalIgnoreCase) || key.Equals("IM", StringComparison.OrdinalIgnoreCase))
                                info.Imei = val;
                            else if (key.Equals("SN", StringComparison.OrdinalIgnoreCase) || key.Equals("Serial", StringComparison.OrdinalIgnoreCase))
                                info.SerialNumber = val;
                            else if (key.Equals("CSC", StringComparison.OrdinalIgnoreCase) || key.Equals("Buyer", StringComparison.OrdinalIgnoreCase))
                                info.Csc = val;
                            else if (key.Equals("SWVER", StringComparison.OrdinalIgnoreCase) || key.Equals("VER", StringComparison.OrdinalIgnoreCase))
                                info.SoftwareVersion = val;
                            else if (key.Equals("OS_VER", StringComparison.OrdinalIgnoreCase) || key.Equals("Android", StringComparison.OrdinalIgnoreCase))
                                info.AndroidVersion = val;
                        }
                    }
                }
                // Format 2: Parentheses Format MN(SM-A515F) or MODEL(SM-A515F)
                else if (trimmed.StartsWith("MN(", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("MODEL(", StringComparison.OrdinalIgnoreCase))
                {
                    info.Model = ExtractValue(trimmed);
                }
                else if (trimmed.StartsWith("IMEI(", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("IM(", StringComparison.OrdinalIgnoreCase))
                {
                    info.Imei = ExtractValue(trimmed);
                }
                else if (trimmed.StartsWith("SN(", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("SERIAL(", StringComparison.OrdinalIgnoreCase))
                {
                    info.SerialNumber = ExtractValue(trimmed);
                }
                else if (trimmed.StartsWith("CSC(", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("BUYER(", StringComparison.OrdinalIgnoreCase))
                {
                    info.Csc = ExtractValue(trimmed);
                }
                else if (trimmed.StartsWith("VER(", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("SW_VER(", StringComparison.OrdinalIgnoreCase))
                {
                    info.SoftwareVersion = ExtractValue(trimmed);
                }
                else if (trimmed.StartsWith("OS_VER(", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("ANDROID(", StringComparison.OrdinalIgnoreCase))
                {
                    info.AndroidVersion = ExtractValue(trimmed);
                }
            }
        }

        public static string ExtractValue(string text)
        {
            var start = text.IndexOf('(');
            var end = text.LastIndexOf(')');
            if (start >= 0 && end > start)
            {
                return text.Substring(start + 1, end - start - 1).Trim();
            }
            return text;
        }

        /// <summary>
        /// Execute full Samsung MTP FRP Bypass workflow via AT Exploit & ADB payload
        /// </summary>
        public async Task<bool> BypassFrpMtpAsync(string portName, string platformToolsPath, CancellationToken cancellationToken = default)
        {
            var adbExe = Path.Combine(platformToolsPath, "adb.exe");
            if (!File.Exists(adbExe))
            {
                Log($"[Error] adb.exe tidak ditemukan di: {adbExe}");
                return false;
            }

            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║          SAMSUNG ONE-CLICK MTP FRP BYPASS ENGINE               ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            // Step 1: Read Device Info
            var deviceInfo = await ReadDeviceInfoAsync(portName);
            if (deviceInfo == null)
            {
                Log("[Error] Gagal membaca info perangkat. Pastikan kabel USB dan driver Samsung terpasang.");
                return false;
            }

            // Step 2: Prompt User to enter Test Mode
            Log("\n[Langkah 1/4] Mempersiapkan Service Mode...");
            OnUserActionRequired?.Invoke("Buka menu Panggilan Darurat (Emergency Call) di layar HP, lalu ketik *#0*# (atau #*88# / *#9900#) untuk masuk ke Service Mode.");
            Log(">>> Silakan buka menu Emergency Call di HP, ketik *#0*# sampai muncul kotak warna/menu sensor.");

            await Task.Delay(2000, cancellationToken);

            // Step 3: Send AT Exploit Activation sequence (Multi-Profile 2022-2025 Standard)
            Log("\n[Langkah 2/4] Mengirim sinyal aktivasi USB Debugging ke modem (Metode Standar & Alternatif)...");
            var atCommands = new[]
            {
                "AT+DUMPCTRL=1,0",
                "AT+DEBUGLVC=0,5",
                "AT+SWATD=0",
                "AT+ACTIVATE=0,0,0",
                "AT+KICOM=1",
                "AT+KICOM=0",
                "AT+KSUART=0",
                "AT+SWAT=1",
                "AT+DEVCONINFO"
            };

            foreach (var cmd in atCommands)
            {
                if (cancellationToken.IsCancellationRequested) return false;
                var res = await SendAtCommandAsync(portName, cmd, 1500);
                Log($"  -> Kirim {cmd} | Respon: {res.Trim().Replace("\r", " ").Replace("\n", " ")}");
                // PERBAIKAN Cacat 1: Delay minimum 600ms antar perintah AT.
                // Samsung modem memerlukan waktu setelah port ditutup untuk melepas handle Windows.
                // Terlalu cepat = IOException 'Access to COM denied' atau port stuck.
                await Task.Delay(600, cancellationToken);
            }

            // Step 4: Monitor & Poll ADB authorization
            Log("\n[Langkah 3/4] Menunggu popup 'Izinkan USB Debugging' di layar HP (Batas waktu: 60 detik)...");
            OnUserActionRequired?.Invoke("Periksa layar HP! Centang 'Selalu izinkan dari komputer ini' lalu tekan tombol OKE / ALLOW.");

            var adbDetected = false;
            var maxWaitSeconds = 60;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < maxWaitSeconds && !cancellationToken.IsCancellationRequested)
            {
                var adbDevicesOutput = await ExecuteAdbCommandAsync(adbExe, "devices");
                var lines = adbDevicesOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1].Trim().Equals("device", StringComparison.OrdinalIgnoreCase))
                    {
                        adbDetected = true;
                        Log($"[OK] Perangkat ADB terdeteksi & terotorisasi: {parts[0].Trim()}");
                        break;
                    }
                }

                if (adbDetected) break;
                await Task.Delay(1500, cancellationToken);
            }

            if (!adbDetected)
            {
                Log("[Gagal] Waktu habis. Otorisasi ADB tidak terdeteksi di HP.");
                Log("[Catatan] Jika HP menggunakan Security Patch terbaru (2023/2024+), coba restart HP dan ulangi kode *#0*# atau gunakan mode EDL / ISP / BROM.");
                return false;
            }

            // Step 5: Execute ADB FRP Exploit & Setup Provisioning
            Log("\n[Langkah 4/4] Mengeksekusi payload FRP bypass via ADB...");

            var adbPayloads = new[]
            {
                "shell settings put global setup_wizard_has_run 1",
                "shell settings put secure user_setup_complete 1",
                "shell settings put global device_provisioned 1",
                "shell content insert --uri content://settings/secure --bind name:s:user_setup_complete --bind value:s:1",
                "shell pm disable-user --user 0 com.google.android.setupwizard",
                "shell pm disable-user --user 0 com.sec.android.app.SecSetupWizard",
                "shell pm disable-user --user 0 com.sec.android.app.setupwizard",
                "shell am start -c android.intent.category.HOME -a android.intent.action.MAIN",
                "shell am start -n com.sec.android.app.launcher/.activities.LauncherActivity",
                "shell am start -a android.settings.SETTINGS"
            };

            // PERBAIKAN Cacat 2: Lacak keberhasilan payload kritis.
            // adb reboot hanya dipanggil jika setup_wizard_has_run + user_setup_complete berhasil diset.
            var criticalPayloadSuccess = 0;
            foreach (var payload in adbPayloads)
            {
                var result = await ExecuteAdbCommandAsync(adbExe, payload);
                var isError = result.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                              result.Contains("Exception", StringComparison.OrdinalIgnoreCase);
                Log($"  -> adb {payload} {(isError ? "[WARN]" : "[OK]")}");

                // Lacak keberhasilan 2 payload kritis
                if ((payload.Contains("setup_wizard_has_run") || payload.Contains("user_setup_complete")) && !isError)
                    criticalPayloadSuccess++;
            }

            if (criticalPayloadSuccess < 2)
            {
                Log("\n[Peringatan] Payload provisioning kritis gagal dikirim. Perangkat TIDAK di-restart otomatis.");
                Log("[Saran] Periksa apakah ADB masih terotorisasi, lalu coba ulang. Restart HP secara manual jika ingin mencoba ulang.");
                return false;
            }

            Log("\n[Mengirim Perintah Restart]");
            await ExecuteAdbCommandAsync(adbExe, "reboot");

            Log("\n════════════════════════════════════════════════════════════════");
            Log(" [SUKSES] Bypass FRP Samsung MTP Berhasil 100%!");
            Log(" Perangkat sedang merestart ke layar utama.");
            Log("════════════════════════════════════════════════════════════════\n");

            return true;
        }

        /// <summary>
        /// Reboot Samsung device via AT Command (Normal or Download mode)
        /// </summary>
        public async Task<bool> RebootAsync(string portName, SamsungRebootMode mode)
        {
            var modeName = mode == SamsungRebootMode.Download ? "Download Mode (Odin)" : "Normal System";
            Log($"Mengirim perintah reboot ke [{modeName}] via port {portName}...");

            var cmd = $"AT+REBOOT={(int)mode}";
            var res = await SendAtCommandAsync(portName, cmd, 2000);
            Log($"Respon: {res.Trim()}");

            return res.Contains("OK");
        }

        private async Task<string> ExecuteAdbCommandAsync(string adbPath, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return string.Empty;

                var output = await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit(5000));
                return output;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
