using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using JumperTools.Core;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== READ INFO PERANGKAT (STANDARD TECHNICIAN CONSOLE) ====================

        private string _infoBrand = "Hubungkan HP lalu klik Baca Info";
        public string InfoBrand { get => _infoBrand; set { _infoBrand = value; OnPropertyChanged(nameof(InfoBrand)); } }

        private string _infoModel = "Menunggu Pembacaan...";
        public string InfoModel { get => _infoModel; set { _infoModel = value; OnPropertyChanged(nameof(InfoModel)); } }

        private string _infoAndroidVer = "-";
        public string InfoAndroidVer { get => _infoAndroidVer; set { _infoAndroidVer = value; OnPropertyChanged(nameof(InfoAndroidVer)); } }

        private string _infoSecurityPatch = "-";
        public string InfoSecurityPatch { get => _infoSecurityPatch; set { _infoSecurityPatch = value; OnPropertyChanged(nameof(InfoSecurityPatch)); } }

        private string _infoBootloader = "-";
        public string InfoBootloader
        {
            get => _infoBootloader;
            set
            {
                _infoBootloader = value;
                OnPropertyChanged(nameof(InfoBootloader));
                OnPropertyChanged(nameof(InfoBootloaderColor));
            }
        }

        public string InfoBootloaderColor
        {
            get
            {
                if (string.IsNullOrEmpty(_infoBootloader) || _infoBootloader == "-") return "#64748B";
                if (_infoBootloader.Contains("Unlocked", StringComparison.OrdinalIgnoreCase)) return "#D97706";
                return "#059669";
            }
        }

        private string _infoGoogleFrp = "-";
        public string InfoGoogleFrp
        {
            get => _infoGoogleFrp;
            set
            {
                _infoGoogleFrp = value;
                OnPropertyChanged(nameof(InfoGoogleFrp));
                OnPropertyChanged(nameof(InfoGoogleFrpColor));
            }
        }

        public string InfoGoogleFrpColor
        {
            get
            {
                if (string.IsNullOrEmpty(_infoGoogleFrp) || _infoGoogleFrp == "-") return "#64748B";
                if (_infoGoogleFrp.Contains("Terkunci", StringComparison.OrdinalIgnoreCase)) return "#EF4444";
                return "#059669";
            }
        }

        private string _infoBattery = "-";
        public string InfoBattery
        {
            get => _infoBattery;
            set
            {
                _infoBattery = value;
                OnPropertyChanged(nameof(InfoBattery));
                OnPropertyChanged(nameof(InfoBatteryColor));
            }
        }

        public string InfoBatteryColor
        {
            get
            {
                if (string.IsNullOrEmpty(_infoBattery) || _infoBattery == "-") return "#64748B";
                return "#10B981";
            }
        }

        private string _infoRam = "-";
        public string InfoRam { get => _infoRam; set { _infoRam = value; OnPropertyChanged(nameof(InfoRam)); } }

        private string _infoConnectionMode = "Auto (ADB/FB/MTP)";
        public string InfoConnectionMode { get => _infoConnectionMode; set { _infoConnectionMode = value; OnPropertyChanged(nameof(InfoConnectionMode)); } }

        private string _infoSerialNumber = "-";
        public string InfoSerialNumber { get => _infoSerialNumber; set { _infoSerialNumber = value; OnPropertyChanged(nameof(InfoSerialNumber)); } }

        private string _readInfoLogText = "════════════════════════════════════════════════════════════════\n" +
                                         " [📖 READ INFO PERANGKAT]\n" +
                                         " Hubungkan handphone ke kabel USB (MTP / ADB / Fastboot / EDL / BROM).\n" +
                                         " Klik tombol [📖 Baca Info Perangkat] untuk membaca data aktual.\n" +
                                         "════════════════════════════════════════════════════════════════\n";
        public string ReadInfoLogText
        {
            get => _readInfoLogText;
            set
            {
                _readInfoLogText = value;
                OnPropertyChanged(nameof(ReadInfoLogText));
            }
        }

        private bool _isReadingInfo = false;
        public bool IsReadingInfo
        {
            get => _isReadingInfo;
            set
            {
                _isReadingInfo = value;
                OnPropertyChanged(nameof(IsReadingInfo));
            }
        }

        public async Task ReadDeviceInfoAsync()
        {
            if (IsReadingInfo) return;

            IsReadingInfo = true;
            CurrentOperation = "Membaca Informasi Perangkat...";
            OperationProgress = 20;

            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════════════");
            sb.AppendLine($" [📖 READ INFO] BACA INFORMASI AKTUAL PERANGKAT — {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════════════════════════════");

            try
            {
                var adbPath = System.IO.Path.Combine(_platformToolsPath, "adb.exe");
                var fastbootPath = System.IO.Path.Combine(_platformToolsPath, "fastboot.exe");

                var serial = SelectedDevice?.DeviceId;
                if (string.IsNullOrEmpty(serial) && Devices.Count > 0)
                {
                    serial = Devices[0].DeviceId;
                }

                // 1. CEK FASTBOOT DEVICES
                var fbOut = await RunProcessAsync(fastbootPath, "devices", 2000);
                if (!string.IsNullOrWhiteSpace(fbOut) && !fbOut.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(" • Mode Koneksi   : FASTBOOT / BOOTLOADER");
                    sb.AppendLine($" • Serial Perangkat: {serial ?? "Fastboot Target"}");
                    sb.AppendLine("────────────────────────────────────────────────────────────────");
                    sb.AppendLine(" [VARIABLE FASTBOOT PERANGKAT]");

                    var fbVars = await RunProcessAsync(fastbootPath, "getvar all", 3000);
                    var lines = fbVars.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int count = 0;
                    foreach (var line in lines)
                    {
                        if (line.Contains("(bootloader)") || line.Contains("getvar:"))
                        {
                            sb.AppendLine($"   {line.Replace("(bootloader)", "").Trim()}");
                            count++;
                        }
                    }
                    if (count == 0 && !string.IsNullOrWhiteSpace(fbVars))
                    {
                        sb.AppendLine($"   {fbVars.Trim()}");
                    }
                }
                else
                {
                    // 2. CEK ADB DEVICES
                    var prefix = !string.IsNullOrEmpty(serial) ? $"-s {serial} " : "";
                    var adbDevs = await RunProcessAsync(adbPath, "devices -l", 2000);

                    if (adbDevs.Contains("device") && !adbDevs.Contains("offline") && !adbDevs.Contains("unauthorized"))
                    {
                        sb.AppendLine(" • Mode Koneksi   : NORMAL (ADB AKTIF)");
                        sb.AppendLine($" • Serial Perangkat: {serial ?? "Target Terhubung"}");
                        sb.AppendLine("────────────────────────────────────────────────────────────────");

                        // IDENTITAS
                        var model = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.product.model", 2000)).Trim();
                        var brand = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.product.brand", 2000)).Trim();
                        var androidVer = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.build.version.release", 2000)).Trim();
                        var spl = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.build.version.security_patch", 2000)).Trim();
                        var buildId = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.build.display.id", 2000)).Trim();
                        var fp = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.build.fingerprint", 2000)).Trim();

                        InfoConnectionMode = "ADB Online";
                        InfoBrand = !string.IsNullOrEmpty(brand) ? brand : "Android";
                        InfoModel = !string.IsNullOrEmpty(model) ? model : "Device";
                        InfoAndroidVer = !string.IsNullOrEmpty(androidVer) ? $"Android {androidVer}" : "-";
                        InfoSecurityPatch = !string.IsNullOrEmpty(spl) ? spl : "-";
                        InfoSerialNumber = serial ?? "-";

                        sb.AppendLine(" [1] IDENTITAS FIRMWARE & HARDWARE");
                        if (!string.IsNullOrEmpty(brand)) sb.AppendLine($"     - Brand / Vendor      : {brand}");
                        if (!string.IsNullOrEmpty(model)) sb.AppendLine($"     - Model / Tipe        : {model}");
                        if (!string.IsNullOrEmpty(androidVer)) sb.AppendLine($"     - Versi Android       : Android {androidVer}");
                        if (!string.IsNullOrEmpty(spl)) sb.AppendLine($"     - Security Patch (SPL): {spl}");
                        if (!string.IsNullOrEmpty(buildId)) sb.AppendLine($"     - Build ID / ROM      : {buildId}");
                        if (!string.IsNullOrEmpty(fp)) sb.AppendLine($"     - Fingerprint         : {fp}");

                        // KEAMANAN & AKUN
                        var bootState = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.boot.verifiedbootstate", 2000)).Trim();
                        var flashLocked = (await RunProcessAsync(adbPath, $"{prefix}shell getprop ro.boot.flash.locked", 2000)).Trim();
                        var accounts = await RunProcessAsync(adbPath, $"{prefix}shell dumpsys account", 3000);
                        var suCheck = await RunProcessAsync(adbPath, $"{prefix}shell which su", 2000);

                        InfoBootloader = (bootState.Equals("orange", StringComparison.OrdinalIgnoreCase) || flashLocked == "0") ? "Unlocked (Orange)" : "Locked (Green)";
                        InfoGoogleFrp = accounts.Contains("type=com.google") ? "Terkunci FRP" : "Bersih (No FRP)";

                        sb.AppendLine("\n [2] STATUS KEAMANAN & KUNCI");
                        if (bootState.Equals("orange", StringComparison.OrdinalIgnoreCase) || flashLocked == "0")
                            sb.AppendLine("     - Bootloader (UBL)    : UNLOCKED (Terbuka / Orange State)");
                        else if (bootState.Equals("green", StringComparison.OrdinalIgnoreCase) || flashLocked == "1")
                            sb.AppendLine("     - Bootloader (UBL)    : LOCKED (Terkunci / Green State)");
                        else if (!string.IsNullOrEmpty(bootState))
                            sb.AppendLine($"     - Bootloader State    : {bootState}");

                        if (accounts.Contains("type=com.google"))
                        {
                            var acctMatches = Regex.Matches(accounts, @"Account\s*\{name=([^,\s]+),\s*type=com\.google\}");
                            sb.AppendLine($"     - Akun Google (FRP)   : TERKUNCI ({acctMatches.Count} Akun Terkait)");
                            foreach (Match m in acctMatches)
                            {
                                sb.AppendLine($"       * {m.Groups[1].Value}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("     - Akun Google (FRP)   : Bersih / Tidak Terkait Akun");
                        }

                        sb.AppendLine(suCheck.Contains("/su")
                            ? "     - Status Root         : ROOTED (Magisk / SuperSU Aktif)"
                            : "     - Status Root         : Bersih (Tidak Ter-root)");

                        // RADIO & SINYAL
                        var bb = (await RunProcessAsync(adbPath, $"{prefix}shell getprop gsm.version.baseband", 2000)).Trim();
                        var sim = (await RunProcessAsync(adbPath, $"{prefix}shell getprop gsm.sim.state", 2000)).Trim();
                        sb.AppendLine("\n [3] RADIO & BASEBAND");
                        if (!string.IsNullOrEmpty(bb)) sb.AppendLine($"     - Versi Baseband      : {bb}");
                        if (!string.IsNullOrEmpty(sim)) sb.AppendLine($"     - Status Kartu SIM    : {sim}");

                        // BATERAI & DAYA
                        var bat = await RunProcessAsync(adbPath, $"{prefix}shell dumpsys battery", 2000);
                        var lvlM = Regex.Match(bat, @"level:\s*(\d+)");
                        var tempM = Regex.Match(bat, @"temperature:\s*(\d+)");
                        var voltM = Regex.Match(bat, @"voltage:\s*(\d+)");
                        var statusM = Regex.Match(bat, @"status:\s*(\d+)");

                        InfoBattery = lvlM.Success ? $"{lvlM.Groups[1].Value}%" : "-";

                        sb.AppendLine("\n [4] BATERAI & DAYA");
                        if (lvlM.Success) sb.AppendLine($"     - Level Baterai       : {lvlM.Groups[1].Value}%");
                        if (tempM.Success && double.TryParse(tempM.Groups[1].Value, out var t)) sb.AppendLine($"     - Suhu Baterai        : {t / 10.0:F1} °C");
                        if (voltM.Success && int.TryParse(voltM.Groups[1].Value, out var v)) sb.AppendLine($"     - Tegangan Baterai    : {(v < 100 ? v * 1000 : v)} mV");
                        if (statusM.Success) sb.AppendLine($"     - Status Pengisian    : {(statusM.Groups[1].Value == "2" ? "Sedang Mengisi Daya (Charging)" : "Tidak Mengisi")}");

                        // MEMORI & STORAGE
                        var mem = await RunProcessAsync(adbPath, $"{prefix}shell cat /proc/meminfo", 2000);
                        var memTotM = Regex.Match(mem, @"MemTotal:\s*(\d+)\s*kB");
                        var memAvailM = Regex.Match(mem, @"MemAvailable:\s*(\d+)\s*kB");

                        long ramTotalKb = 0;
                        if (memTotM.Success && long.TryParse(memTotM.Groups[1].Value, out ramTotalKb))
                        {
                            InfoRam = $"{ramTotalKb / (1024.0 * 1024.0):F1} GB";
                        }
                        else
                        {
                            InfoRam = "-";
                        }

                        sb.AppendLine("\n [5] KAPASITAS MEMORI");
                        if (ramTotalKb > 0)
                            sb.AppendLine($"     - Total RAM Fisik     : {ramTotalKb / (1024.0 * 1024.0):F2} GB");
                        if (memAvailM.Success && long.TryParse(memAvailM.Groups[1].Value, out var avKb))
                            sb.AppendLine($"     - RAM Bebas (Avail)   : {avKb / (1024.0 * 1024.0):F2} GB");

                        // DISPLAY
                        var wm = (await RunProcessAsync(adbPath, $"{prefix}shell wm size", 2000)).Trim();
                        var dens = (await RunProcessAsync(adbPath, $"{prefix}shell wm density", 2000)).Trim();
                        sb.AppendLine("\n [6] DISPLAY / PANEL");
                        if (!string.IsNullOrEmpty(wm)) sb.AppendLine($"     - Resolusi Layar      : {wm}");
                        if (!string.IsNullOrEmpty(dens)) sb.AppendLine($"     - Densitas Layar      : {dens}");
                    }
                    else
                    {
                        // 3. MTP / PNP DEVICE MODE
                        InfoConnectionMode = "MTP Bus";
                        InfoModel = SelectedDevice?.Model ?? serial ?? "Perangkat USB";
                        InfoSerialNumber = SelectedDevice?.SerialNumber ?? serial ?? "-";

                        sb.AppendLine(" • Mode Koneksi   : MTP / WINDOWS USB BUS (Zero-ADB)");
                        sb.AppendLine($" • Nama Model / SN: {SelectedDevice?.Model ?? serial ?? "Perangkat USB"}");
                        sb.AppendLine("────────────────────────────────────────────────────────────────");
                        sb.AppendLine(" [INFO HARDWARE BUS USB]");
                        sb.AppendLine($"   - Model Deskriptor : {SelectedDevice?.Model ?? "Android Smartphone"}");
                        sb.AppendLine($"   - Serial Number    : {SelectedDevice?.SerialNumber ?? serial ?? "-"}");
                        sb.AppendLine("   - Status Debugging : Nonaktif / Terkunci Layar Sandi / Setup Wizard FRP");
                        sb.AppendLine("   - Aksi Tersedia    : Tekan tombol [⚡ Bypass FRP (Browser)] untuk membuka YouTube/Browser di HP,");
                        sb.AppendLine("                        atau matikan HP lalu tekan [Vol Bawah + Power] untuk masuk Fastboot.");
                    }
                }

                sb.AppendLine("────────────────────────────────────────────────────────────────");
                sb.AppendLine(" [✔] Operasi Read Info Selesai.");
                sb.AppendLine("════════════════════════════════════════════════════════════════");

                ReadInfoLogText = sb.ToString();
                AddLog("[📖 READ INFO] Informasi perangkat berhasil dimuat.");
                CurrentOperation = "Read Info Selesai";
                OperationProgress = 100;
            }
            catch (Exception ex)
            {
                sb.AppendLine($"\n[❌ ERROR] Gagal membaca perangkat: {ex.Message}");
                sb.AppendLine("════════════════════════════════════════════════════════════════");
                ReadInfoLogText = sb.ToString();
                AddLog($"[📖 READ INFO Error] {ex.Message}");
                CurrentOperation = "Read Info Gagal";
                OperationProgress = 100;
            }
            finally
            {
                IsReadingInfo = false;
            }
        }

        private async Task<string> RunProcessAsync(string exePath, string arguments, int timeoutMs = 3000)
        {
            try
            {
                if (!System.IO.File.Exists(exePath)) return "";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var proc = new System.Diagnostics.Process { StartInfo = psi };
                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();

                proc.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                var completed = await Task.Run(() => proc.WaitForExit(timeoutMs));
                if (completed)
                {
                    proc.WaitForExit();
                    return sbOut.ToString() + "\n" + sbErr.ToString();
                }
                else
                {
                    try { proc.Kill(true); } catch { }
                    return sbOut.ToString();
                }
            }
            catch
            {
                return "";
            }
        }

        public void CopyReadInfoLog()
        {
            try
            {
                Clipboard.SetText(ReadInfoLogText);
                AddLog("[📖 READ INFO] Teks informasi perangkat disalin ke clipboard.");
            }
            catch { }
        }

        public void ClearReadInfoLog()
        {
            ReadInfoLogText = "════════════════════════════════════════════════════════════════\n" +
                              " [📖 READ INFO PERANGKAT]\n" +
                              " Log telah dibersihkan. Klik [📖 Baca Info Perangkat] untuk membaca ulang.\n" +
                              "════════════════════════════════════════════════════════════════\n";
        }
    }
}
