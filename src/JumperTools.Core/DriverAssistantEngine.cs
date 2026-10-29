using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace JumperTools.Core
{
    public class DriverStatusItem : INotifyPropertyChanged
    {
        private string _driverName = string.Empty;
        private string _targetChipset = string.Empty;
        private string _hardwareId = string.Empty;
        private string _installerFileName = string.Empty;
        private bool _isInstalled;
        private bool _isActiveConnected;
        private bool _isInstalling;
        private string _statusDescription = "Belum Terpasang";
        private string _category = "USB Port Driver";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string InstallerFileName
        {
            get => _installerFileName;
            set { if (_installerFileName != value) { _installerFileName = value; OnPropertyChanged(); } }
        }

        public string DriverName
        {
            get => _driverName;
            set { if (_driverName != value) { _driverName = value; OnPropertyChanged(); } }
        }

        public string TargetChipset
        {
            get => _targetChipset;
            set { if (_targetChipset != value) { _targetChipset = value; OnPropertyChanged(); } }
        }

        public string HardwareId
        {
            get => _hardwareId;
            set { if (_hardwareId != value) { _hardwareId = value; OnPropertyChanged(); } }
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusDotColor));
                    OnPropertyChanged(nameof(StatusBadgeBgColor));
                    OnPropertyChanged(nameof(StatusBadgeBorderColor));
                    OnPropertyChanged(nameof(StatusBadgeTextColor));
                    OnPropertyChanged(nameof(ShowInstallButton));
                    OnPropertyChanged(nameof(IsInstallEnabled));
                }
            }
        }

        public bool IsActiveConnected
        {
            get => _isActiveConnected;
            set
            {
                if (_isActiveConnected != value)
                {
                    _isActiveConnected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusDotColor));
                    OnPropertyChanged(nameof(StatusBadgeBgColor));
                    OnPropertyChanged(nameof(StatusBadgeBorderColor));
                    OnPropertyChanged(nameof(StatusBadgeTextColor));
                    OnPropertyChanged(nameof(ShowInstallButton));
                }
            }
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set
            {
                if (_isInstalling != value)
                {
                    _isInstalling = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusDotColor));
                    OnPropertyChanged(nameof(StatusBadgeBgColor));
                    OnPropertyChanged(nameof(StatusBadgeBorderColor));
                    OnPropertyChanged(nameof(StatusBadgeTextColor));
                    OnPropertyChanged(nameof(ShowInstallButton));
                    OnPropertyChanged(nameof(InstallButtonText));
                    OnPropertyChanged(nameof(IsInstallEnabled));
                }
            }
        }

        public string StatusDescription
        {
            get => _statusDescription;
            set { if (_statusDescription != value) { _statusDescription = value; OnPropertyChanged(); } }
        }

        public string Category
        {
            get => _category;
            set { if (_category != value) { _category = value; OnPropertyChanged(); } }
        }

        public string StatusBadgeText
        {
            get
            {
                if (IsInstalling) return "Sedang Memasang...";
                if (HardwareId == "USBDK_SERVICE")
                {
                    return IsInstalled ? "Filter UsbDK Aktif" : "Belum Terpasang";
                }
                if (HardwareId == "LIBUSB_FILTER")
                {
                    return IsInstalled ? "WinUSB / LibUSB Aktif" : "Default Windows";
                }
                if (IsActiveConnected && IsInstalled) return "Terhubung & Aktif";
                if (IsActiveConnected && !IsInstalled) return "Error Driver (Colok)";
                if (IsInstalled) return "Driver Siap (Standby)";
                return "Belum Terpasang";
            }
        }

        public string StatusDotColor
        {
            get
            {
                if (IsInstalling) return "#F59E0B"; // Amber
                if (IsActiveConnected && IsInstalled) return "#10B981"; // Emerald Green
                if (IsInstalled) return "#0284C7";       // Sky Blue
                return "#94A3B8";                        // Neutral Slate Dot
            }
        }

        public string StatusBadgeBgColor => "#F8FAFC";

        public string StatusBadgeBorderColor => "#CBD5E1";

        public string StatusBadgeTextColor
        {
            get
            {
                if (IsInstalling) return "#D97706";
                if (IsActiveConnected && IsInstalled) return "#059669";
                if (IsInstalled) return "#0284C7";
                return "#64748B";
            }
        }

        public bool ShowInstallButton => true;
        public string InstallButtonText => IsInstalling ? "Memasang..." : (IsInstalled ? "Install Ulang" : "Install");
        public bool IsInstallEnabled => !IsInstalling;
    }

    /// <summary>
    /// Core Engine for diagnosing, scanning, and assisting Windows USB Drivers for Smartphone Servicing
    /// Includes UsbDK (USB Development Kit) and Libusb filter driver detection for BROM/EDL stability
    /// </summary>
    public class DriverAssistantEngine
    {
        public event Action<string>? OnLog;

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Scan all critical smartphone service drivers on the Windows PC
        /// </summary>
        public async Task<List<DriverStatusItem>> CheckAllDriversAsync()
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║     PEMERIKSAAN KESEHATAN DRIVER & FILTER USB WINDOWS          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            return await Task.Run(() =>
            {
                var drivers = new List<DriverStatusItem>
                {
                    new DriverStatusItem { DriverName = "Qualcomm QDLoader 9008 Driver", TargetChipset = "Qualcomm Snapdragon", HardwareId = "VID_05C6&PID_9008", Category = "COM Port Driver", InstallerFileName = "Qualcomm_QDLoader_Driver_Setup.exe" },
                    new DriverStatusItem { DriverName = "MediaTek VCOM / BROM Driver", TargetChipset = "MediaTek Helio / Dimensity", HardwareId = "VID_0E8D", Category = "COM Port Driver", InstallerFileName = "MediaTek_VCOM_Driver_Setup.exe" },
                    new DriverStatusItem { DriverName = "Unisoc / SPRD USB Serial Driver", TargetChipset = "Unisoc Tiger / SC9863A", HardwareId = "VID_1782", Category = "COM Port Driver", InstallerFileName = "Unisoc_SPRD_Driver_Setup.exe" },
                    new DriverStatusItem { DriverName = "Samsung Mobile USB Modem Driver", TargetChipset = "Samsung Exynos / Qualcomm", HardwareId = "VID_04E8", Category = "Modem AT Driver", InstallerFileName = "Samsung_USB_Driver_Setup.exe" },
                    new DriverStatusItem { DriverName = "Android ADB & Fastboot Interface", TargetChipset = "Universal Android Interface", HardwareId = "VID_18D1", Category = "USB Debugging", InstallerFileName = "Android_ADB_Driver_Setup.msi" },
                    new DriverStatusItem { DriverName = "Apple Mobile Device USB Driver", TargetChipset = "Apple iPhone / iPad", HardwareId = "VID_05AC", Category = "DFU / Recovery", InstallerFileName = "Apple_Mobile_Device_Driver_Setup.exe" },
                    new DriverStatusItem { DriverName = "UsbDK (USB Development Kit Filter)", TargetChipset = "BROM / EDL Interception Filter", HardwareId = "USBDK_SERVICE", Category = "Filter Driver", InstallerFileName = "UsbDK_Filter_x64_Setup.msi" },
                    new DriverStatusItem { DriverName = "WinUSB / Libusb-1.0 Filter", TargetChipset = "Raw USB Packet Exploit", HardwareId = "LIBUSB_FILTER", Category = "Filter Driver", InstallerFileName = "WinUSB_Zadig_Setup.exe" }
                };

                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
                    var pnpList = searcher.Get().Cast<ManagementBaseObject>().ToList();

                    foreach (var d in drivers)
                    {
                        if (d.HardwareId == "USBDK_SERVICE")
                        {
                            var isUsbDkInstalled = CheckUsbDkInstalled();
                            d.IsInstalled = isUsbDkInstalled;
                            d.IsActiveConnected = isUsbDkInstalled;
                            d.StatusDescription = isUsbDkInstalled 
                                ? "Terpasang (Filter Intersepsi Aktif & Stabil)" 
                                : "Belum Terpasang (Direkomendasikan untuk MTK BROM Anti-Disconnect)";
                            Log($" [{(isUsbDkInstalled ? "OK" : "INFO")}] {d.DriverName.PadRight(35)} : {(isUsbDkInstalled ? "TERPASANG" : "OPSIONAL / DISARANKAN")}");
                            continue;
                        }

                        if (d.HardwareId == "LIBUSB_FILTER")
                        {
                            var hasLibUsb = CheckLibUsbInstalled();
                            d.IsInstalled = hasLibUsb;
                            d.IsActiveConnected = hasLibUsb;
                            d.StatusDescription = hasLibUsb 
                                ? "Terpasang (WinUSB Direct Control)" 
                                : "Standar Windows USB Driver";
                            Log($" [{(hasLibUsb ? "OK" : "INFO")}] {d.DriverName.PadRight(35)} : {(hasLibUsb ? "TERPASANG" : "DEFAULT")}");
                            continue;
                        }

                        // 1. Check actively connected USB device
                        var match = pnpList.FirstOrDefault(p =>
                        {
                            var pnpId = p["PNPDeviceID"]?.ToString() ?? "";
                            var caption = p["Caption"]?.ToString() ?? "";
                            var name = p["Name"]?.ToString() ?? "";
                            return pnpId.Contains(d.HardwareId, StringComparison.OrdinalIgnoreCase) ||
                                   caption.Contains(d.TargetChipset, StringComparison.OrdinalIgnoreCase) ||
                                   name.Contains(d.TargetChipset, StringComparison.OrdinalIgnoreCase);
                        });

                        if (match != null)
                        {
                            var errCode = Convert.ToInt32(match["ConfigManagerErrorCode"] ?? 0);
                            var service = match["Service"]?.ToString() ?? "";
                            var status = match["Status"]?.ToString() ?? "";

                            if (errCode == 0 && !string.IsNullOrEmpty(service) && status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                            {
                                d.IsInstalled = true;
                                d.IsActiveConnected = true;
                                d.StatusDescription = $"Terpasang & Aktif ({match["Caption"]})";
                                Log($" [AKTIF] {d.DriverName.PadRight(35)} : TERHUBUNG & SIAP");
                            }
                            else
                            {
                                d.IsInstalled = false;
                                d.IsActiveConnected = true;
                                d.StatusDescription = $"⚠️ Terhubung TAPI Driver Belum Terpasang (Error {errCode} / Tanda Seru Kuning)";
                                Log($" [ERROR] {d.DriverName.PadRight(35)} : DRIVER BELUM TERPASANG (Code {errCode})");
                            }
                        }
                        else
                        {
                            // 2. Check if driver is already registered in Windows DriverStore / System Drivers
                            var isRegistered = CheckDriverStorePresence(d.HardwareId, d.TargetChipset);
                            if (isRegistered)
                            {
                                d.IsInstalled = true;
                                d.IsActiveConnected = false;
                                d.StatusDescription = "Driver Terpasang di Windows (Standby menunggu HP dicolok)";
                                Log($" [SIAP]  {d.DriverName.PadRight(35)} : TERPASANG (STANDBY)");
                            }
                            else
                            {
                                d.IsInstalled = false;
                                d.IsActiveConnected = false;
                                d.StatusDescription = "Belum Terpasang di PC (Perlu Pemasangan Driver)";
                                Log($" [BELUM] {d.DriverName.PadRight(35)} : BELUM TERPASANG");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Driver Error] Gagal memindai driver: {ex.Message}");
                }

                Log("────────────────────────────────────────────────────────────────");
                Log("[Petunjuk] Driver yang berstatus 'Driver Siap (Standby)' akan otomatis mengenali HP saat dicolokkan.");

                return drivers;
            });
        }

        private static bool CheckDriverStorePresence(string hardwareId, string chipset)
        {
            try
            {
                // 1. Check Registry DriverDatabase Packages (Multi-SoC pattern match)
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\DriverDatabase\DriverPackages"))
                {
                    if (key != null)
                    {
                        var subKeyNames = key.GetSubKeyNames();
                        if (MatchesDriverDatabase(subKeyNames, hardwareId, chipset))
                        {
                            return true;
                        }
                    }
                }

                // 2. Check Installed Software Registry (Samsung, Apple, UsbDk, LibUSB)
                if (CheckInstalledProgramsRegistry(hardwareId, chipset))
                {
                    return true;
                }

                // 3. Check DriverStore FileRepository folder
                var fileRepo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "DriverStore", "FileRepository");
                if (Directory.Exists(fileRepo))
                {
                    var patterns = GetDirectorySearchPatterns(hardwareId, chipset);
                    foreach (var pattern in patterns)
                    {
                        if (Directory.EnumerateDirectories(fileRepo, pattern).Any())
                        {
                            return true;
                        }
                    }
                }

                // 4. Check System32 drivers binary
                var sys32Drivers = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers");
                var sysBinaries = GetSysDriverFiles(hardwareId, chipset);
                foreach (var binary in sysBinaries)
                {
                    if (File.Exists(Path.Combine(sys32Drivers, binary)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Fail gracefully if permissions are restricted
            }
            return false;
        }

        private static bool MatchesDriverDatabase(string[] subKeyNames, string hardwareId, string chipset)
        {
            if (hardwareId.Contains("05C6", StringComparison.OrdinalIgnoreCase) || chipset.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase))
            {
                return subKeyNames.Any(k => k.StartsWith("qcser", StringComparison.OrdinalIgnoreCase) || 
                                            k.StartsWith("qcusbser", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("qcdiag", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("qcadb", StringComparison.OrdinalIgnoreCase));
            }
            if (hardwareId.Contains("0E8D", StringComparison.OrdinalIgnoreCase) || chipset.Contains("MediaTek", StringComparison.OrdinalIgnoreCase))
            {
                return subKeyNames.Any(k => k.StartsWith("cdc-acm", StringComparison.OrdinalIgnoreCase) || 
                                            k.StartsWith("usb2vcom", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("wcnfs20", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("mtk", StringComparison.OrdinalIgnoreCase));
            }
            if (hardwareId.Contains("1782", StringComparison.OrdinalIgnoreCase) || chipset.Contains("Unisoc", StringComparison.OrdinalIgnoreCase))
            {
                return subKeyNames.Any(k => k.StartsWith("sprd", StringComparison.OrdinalIgnoreCase) || 
                                            k.StartsWith("unisoc", StringComparison.OrdinalIgnoreCase));
            }
            if (hardwareId.Contains("04E8", StringComparison.OrdinalIgnoreCase) || chipset.Contains("Samsung", StringComparison.OrdinalIgnoreCase))
            {
                return subKeyNames.Any(k => k.StartsWith("ssud", StringComparison.OrdinalIgnoreCase) || 
                                            k.StartsWith("flashusb", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("samsung", StringComparison.OrdinalIgnoreCase));
            }
            if (hardwareId.Contains("18D1", StringComparison.OrdinalIgnoreCase) || chipset.Contains("Android", StringComparison.OrdinalIgnoreCase))
            {
                return subKeyNames.Any(k => k.StartsWith("android_winusb", StringComparison.OrdinalIgnoreCase) || 
                                            k.StartsWith("androidusb", StringComparison.OrdinalIgnoreCase) ||
                                            k.StartsWith("wceisusb", StringComparison.OrdinalIgnoreCase));
            }
            if (hardwareId.Contains("05AC", StringComparison.OrdinalIgnoreCase) || chipset.Contains("Apple", StringComparison.OrdinalIgnoreCase))
            {
                return subKeyNames.Any(k => k.StartsWith("usbaapl", StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private static bool CheckInstalledProgramsRegistry(string hardwareId, string chipset)
        {
            var uninstallPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var rootPath in uninstallPaths)
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(rootPath);
                if (baseKey == null) continue;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    using var subKey = baseKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName")?.ToString() ?? "";
                    if (string.IsNullOrEmpty(displayName)) continue;

                    if ((hardwareId.Contains("04E8") || chipset.Contains("Samsung")) && displayName.Contains("SAMSUNG", StringComparison.OrdinalIgnoreCase) && displayName.Contains("Driver", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if ((hardwareId.Contains("05AC") || chipset.Contains("Apple")) && displayName.Contains("Apple Mobile Device", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (hardwareId.Contains("05C6") && displayName.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase) && displayName.Contains("Driver", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (hardwareId.Contains("0E8D") && displayName.Contains("MediaTek", StringComparison.OrdinalIgnoreCase) && displayName.Contains("Driver", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (hardwareId.Contains("1782") && displayName.Contains("Unisoc", StringComparison.OrdinalIgnoreCase) || displayName.Contains("Spreadtrum", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private static string[] GetDirectorySearchPatterns(string hardwareId, string chipset)
        {
            if (hardwareId.Contains("05C6") || chipset.Contains("Qualcomm")) return new[] { "qcser*", "qcusbser*", "qcadb*" };
            if (hardwareId.Contains("0E8D") || chipset.Contains("MediaTek")) return new[] { "cdc-acm*", "usb2vcom*", "mtk*" };
            if (hardwareId.Contains("1782") || chipset.Contains("Unisoc")) return new[] { "sprd*", "unisoc*" };
            if (hardwareId.Contains("04E8") || chipset.Contains("Samsung")) return new[] { "ssud*", "flashusb*" };
            if (hardwareId.Contains("18D1") || chipset.Contains("Android")) return new[] { "android_winusb*", "wceisusb*" };
            if (hardwareId.Contains("05AC") || chipset.Contains("Apple")) return new[] { "usbaapl*" };
            return Array.Empty<string>();
        }

        private static string[] GetSysDriverFiles(string hardwareId, string chipset)
        {
            if (hardwareId.Contains("05C6") || chipset.Contains("Qualcomm")) return new[] { "qcser.sys", "qcusbser.sys", "qcadb.sys" };
            if (hardwareId.Contains("0E8D") || chipset.Contains("MediaTek")) return new[] { "usb2vcom.sys", "cdc-acm.sys", "wcnfs20.sys" };
            if (hardwareId.Contains("1782") || chipset.Contains("Unisoc")) return new[] { "SprdSerial.sys", "sprdu2s.sys" };
            if (hardwareId.Contains("04E8") || chipset.Contains("Samsung")) return new[] { "ssudmdm.sys", "ssudbus.sys", "FlashUsb.sys" };
            if (hardwareId.Contains("18D1") || chipset.Contains("Android")) return new[] { "androidusb.sys", "wceisusb.sys" };
            if (hardwareId.Contains("05AC") || chipset.Contains("Apple")) return new[] { "usbaapl64.sys", "usbaapl.sys" };
            return Array.Empty<string>();
        }

        public static bool CheckUsbDkInstalled()
        {
            try
            {
                var sysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "UsbDk.sys");
                if (File.Exists(sysPath)) return true;

                using (var scKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\UsbDk"))
                {
                    if (scKey != null) return true;
                }

                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_SystemDriver WHERE Name = 'UsbDk' OR Name = 'UsbDkHelper'");
                using var results = searcher.Get();
                return results.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool CheckLibUsbInstalled()
        {
            try
            {
                var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (File.Exists(Path.Combine(sys32, "drivers", "libusb0.sys")) || File.Exists(Path.Combine(sys32, "drivers", "libusbK.sys")))
                    return true;

                using (var libusbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\libusb0"))
                {
                    if (libusbKey != null) return true;
                }
                using (var libusbKKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\libusbK"))
                {
                    if (libusbKKey != null) return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Installs UsbDk silently in the background
        /// </summary>
        public async Task<OperationResult> InstallUsbDkAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log("[Installer] Memulai pemasangan UsbDk secara otomatis...");
                    
                    var candidatePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "drivers", "UsbDK_Filter_x64_Setup.msi"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "drivers", "UsbDk64.msi")
                    };

                    var installerPath = candidatePaths.FirstOrDefault(File.Exists);
                    
                    if (string.IsNullOrEmpty(installerPath))
                    {
                        return new OperationResult { Success = false, ErrorMessage = "Installer UsbDk tidak ditemukan di folder tools/drivers/. Harap pastikan UsbDK_Filter_x64_Setup.msi tersedia." };
                    }

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "msiexec.exe",
                            Arguments = $"/i \"{installerPath}\" /quiet /norestart",
                            UseShellExecute = true,
                            Verb = "runas",
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode == 0 || process.ExitCode == 1641 || process.ExitCode == 3010)
                    {
                        Log("[Installer] UsbDk berhasil dipasang! (Filter driver siap digunakan)");
                        return new OperationResult { Success = true };
                    }
                    
                    return new OperationResult { Success = false, ErrorMessage = $"Pemasangan gagal dengan kode: {process.ExitCode}" };
                }
                catch (Exception ex)
                {
                    Log($"[Error] Pemasangan UsbDk gagal: {ex.Message}");
                    return new OperationResult { Success = false, ErrorMessage = ex.Message };
                }
            });
        }

        /// <summary>
        /// Launch Zadig USB Driver installer for 1-click WinUSB / libusb driver fixing
        /// </summary>
        public async Task<OperationResult> LaunchZadigAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var candidates = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "drivers", "WinUSB_Zadig_Setup.exe"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "drivers", "zadig.exe"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "drivers", "WinUSB_Zadig_Setup.exe")
                    };

                    var zadigPath = candidates.FirstOrDefault(File.Exists);

                    if (string.IsNullOrEmpty(zadigPath))
                    {
                        return new OperationResult { Success = false, ErrorMessage = "Zadig driver installer tidak ditemukan di tools/drivers/WinUSB_Zadig_Setup.exe." };
                    }

                    Log("[Zadig] Membuka Zadig USB Driver Assistant...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = zadigPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    });

                    return new OperationResult { Success = true };
                }
                catch (Exception ex)
                {
                    return new OperationResult { Success = false, ErrorMessage = ex.Message };
                }
            });
        }

        /// <summary>
        /// Installs LibUSB filter driver silently into Windows
        /// </summary>
        public async Task<OperationResult> InstallLibUsbAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log("[Installer] Membuka wizard konfigurasi LibUSB/WinUSB...");
                    return LaunchZadigAsync().Result;
                }
                catch (Exception ex)
                {
                    Log($"[Error] Pemasangan LibUSB gagal: {ex.Message}");
                    return new OperationResult { Success = false, ErrorMessage = ex.Message };
                }
            });
        }


        /// <summary>
        /// Installs a driver from an INF file directly into Windows DriverStore using pnputil.exe
        /// </summary>
        public async Task<OperationResult> InstallInfDriverAsync(string infPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(infPath))
                    {
                        return new OperationResult { Success = false, ErrorMessage = $"File INF tidak ditemukan: {infPath}" };
                    }

                    Log($"[pnputil] Memasang berkas driver INF ke Windows: {Path.GetFileName(infPath)}...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = $"/add-driver \"{infPath}\" /install",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        p.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log($" [pnputil] {e.Data.Trim()}"); };
                        p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log($" [pnputil error] {e.Data.Trim()}"); };
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.WaitForExit();

                        if (p.ExitCode == 0 || p.ExitCode == 3010 || p.ExitCode == 259)
                        {
                            Log($"[pnputil] ✅ Driver {Path.GetFileName(infPath)} berhasil terpasang di Windows DriverStore.");
                            return new OperationResult { Success = true };
                        }
                        return new OperationResult { Success = false, ErrorMessage = $"pnputil keluar dengan kode: {p.ExitCode}" };
                    }
                    return new OperationResult { Success = false, ErrorMessage = "Gagal menjalankan pnputil.exe" };
                }
                catch (Exception ex)
                {
                    Log($"[pnputil Error] {ex.Message}");
                    return new OperationResult { Success = false, ErrorMessage = ex.Message };
                }
            });
        }

        /// <summary>
        /// Installs all driver INF files from a base directory recursively in ONE single process
        /// </summary>
        public async Task<OperationResult> InstallAllInfDriversBatchAsync(string driversFolder)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(driversFolder))
                    {
                        return new OperationResult { Success = false, ErrorMessage = $"Direktori drivers tidak ditemukan: {driversFolder}" };
                    }

                    Log($"[pnputil] ⚡ Mendaftarkan seluruh paket driver INF dari folder: {driversFolder}...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = $"/add-driver \"{Path.Combine(driversFolder, "*.inf")}\" /subdirs /install",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        p.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log($" [pnputil] {e.Data.Trim()}"); };
                        p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Log($" [pnputil error] {e.Data.Trim()}"); };
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.WaitForExit();

                        Log($"[pnputil] ✅ Seluruh paket driver selesai diproses oleh Windows (ExitCode: {p.ExitCode}).");
                        return new OperationResult { Success = true };
                    }
                    return new OperationResult { Success = false, ErrorMessage = "Gagal menjalankan pnputil batch" };
                }
                catch (Exception ex)
                {
                    Log($"[pnputil Error] {ex.Message}");
                    return new OperationResult { Success = false, ErrorMessage = ex.Message };
                }
            });
        }

        /// <summary>
        /// Open Windows Device Manager (devmgmt.msc)
        /// </summary>
        public void OpenDeviceManager()
        {
            try
            {
                Log("[Windows] Membuka Windows Device Manager (devmgmt.msc)...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "devmgmt.msc",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log($"[Error] Tidak dapat membuka Device Manager: {ex.Message}");
            }
        }
    }
}

