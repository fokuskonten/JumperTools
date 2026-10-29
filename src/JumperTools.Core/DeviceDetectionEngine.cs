using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Core engine for detecting and managing Android devices via ADB
    /// </summary>
    public class DeviceDetectionEngine
    {
        private readonly string _adbPath;
        private readonly string _fastbootPath;

        public DeviceDetectionEngine(string platformToolsPath)
        {
            _adbPath = Path.Combine(platformToolsPath, "adb.exe");
            _fastbootPath = Path.Combine(platformToolsPath, "fastboot.exe");
        }

        /// <summary>
        /// Get all connected devices via ADB
        /// </summary>
        public async Task<List<Device>> GetConnectedDevicesAsync()
        {
            var devices = new List<Device>();

            try
            {
                var result = await ExecuteCommandAsync(_adbPath, "devices");
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines.Skip(1)) // Skip first line (header)
                {
                    var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var deviceId = parts[0].Trim();
                        var status = parts[1].Trim();

                        var device = await GetDeviceInfoAsync(deviceId);
                        device.Status = ParseDeviceStatus(status);
                        if (status.Equals("sideload", StringComparison.OrdinalIgnoreCase))
                        {
                            device.Mode = DeviceMode.Recovery;
                            device.Brand = "Xiaomi";
                            device.Model = "Xiaomi / Redmi (Mi Assistant Sideload)";
                            device.Capabilities = new DeviceCapabilities
                            {
                                SupportsRecovery = true,
                                SupportedOperations = new[] { "Read Info", "Format Userdata", "Bypass FRP", "Reboot System" }
                            };
                        }
                        else
                        {
                            device.Mode = DeviceMode.Normal;
                        }
                        device.LastSeen = DateTime.UtcNow;

                        devices.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices in fastboot mode (100% Ground Truth from fastboot.exe)
        /// </summary>
        public async Task<List<Device>> GetFastbootDevicesAsync()
        {
            var devices = new List<Device>();

            try
            {
                var result = await ExecuteCommandAsync(_fastbootPath, "devices");
                var lines = result.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                    var parts = trimmed.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1].Equals("fastboot", StringComparison.OrdinalIgnoreCase))
                    {
                        var deviceId = parts[0].Trim();
                        var device = await GetFastbootDeviceInfoAsync(deviceId);
                        device.Mode = DeviceMode.Fastboot;
                        device.Status = DeviceStatus.Connected;
                        device.LastSeen = DateTime.UtcNow;

                        devices.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting fastboot devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices connected via Samsung Modem COM Ports
        /// </summary>
        public async Task<List<Device>> GetSamsungComDevicesAsync()
        {
            var devices = new List<Device>();

            try
            {
                var ports = await Task.Run(() => SamsungAtEngine.ScanSamsungPorts());
                foreach (var p in ports)
                {
                    var device = new Device
                    {
                        DeviceId = p.PortName,
                        SerialNumber = p.PortName,
                        Model = "Samsung Galaxy (MTP / Modem)",
                        Brand = "Samsung",
                        Manufacturer = "Samsung",
                        Mode = DeviceMode.Normal,
                        Status = DeviceStatus.Connected,
                        LastSeen = DateTime.UtcNow,
                        Capabilities = new DeviceCapabilities
                        {
                            SupportsOdin = true,
                            SupportsDownloadMode = true,
                            SupportedOperations = new[] { "MTP Bypass FRP", "Read Info", "Reboot Download", "Reboot Normal" }
                        }
                    };

                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting Samsung COM devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices connected via MediaTek USB Port (BROM / Preloader)
        /// </summary>
        public async Task<List<Device>> GetMediaTekPortsAsync()
        {
            var devices = new List<Device>();

            try
            {
                var ports = await Task.Run(() => MediaTekBromEngine.ScanMtkPorts());
                foreach (var p in ports)
                {
                    var device = new Device
                    {
                        DeviceId = p.PortName,
                        SerialNumber = p.PortName,
                        Model = p.IsBromMode ? "MediaTek Device (BROM Mode)" : "MediaTek Device (Preloader Mode)",
                        Brand = "MediaTek",
                        Manufacturer = "MediaTek",
                        Mode = DeviceMode.Download,
                        Status = DeviceStatus.Connected,
                        LastSeen = DateTime.UtcNow,
                        Capabilities = new DeviceCapabilities
                        {
                            SupportsDownloadMode = true,
                            SupportedOperations = new[] { "Bypass FRP", "Format Userdata", "Instant UBL", "Read GPT", "Backup NVRAM" }
                        }
                    };

                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting MTK devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices connected via Qualcomm HS-USB QDLoader 9008 COM Port
        /// </summary>
        public async Task<List<Device>> GetQualcommPortsAsync()
        {
            var devices = new List<Device>();

            try
            {
                var ports = await Task.Run(() => QualcommEdlEngine.ScanQualcommPorts());
                foreach (var p in ports)
                {
                    var device = new Device
                    {
                        DeviceId = p.PortName,
                        SerialNumber = p.PortName,
                        Model = "Qualcomm Snapdragon (EDL 9008)",
                        Brand = "Qualcomm",
                        Manufacturer = "Qualcomm",
                        Mode = DeviceMode.EDL,
                        Status = DeviceStatus.Connected,
                        LastSeen = DateTime.UtcNow,
                        Capabilities = new DeviceCapabilities
                        {
                            SupportsEdl = true,
                            SupportedOperations = new[] { "Bypass FRP", "Format Userdata", "Read GPT", "Backup EFS/QCN", "Reboot Reset" }
                        }
                    };

                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting Qualcomm EDL devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices connected via Unisoc / SPRD USB Serial Port (VID 0x1782)
        /// </summary>
        public async Task<List<Device>> GetUnisocPortsAsync()
        {
            var devices = new List<Device>();

            try
            {
                var ports = await Task.Run(() => UnisocSpdEngine.ScanUnisocPorts());
                foreach (var p in ports)
                {
                    var device = new Device
                    {
                        DeviceId = p.PortName,
                        SerialNumber = p.PortName,
                        Model = "Unisoc / Spreadtrum Tiger (SPRD BSL)",
                        Brand = "Unisoc",
                        Manufacturer = "Unisoc",
                        Mode = DeviceMode.Download,
                        Status = DeviceStatus.Connected,
                        LastSeen = DateTime.UtcNow,
                        Capabilities = new DeviceCapabilities
                        {
                            SupportsDownloadMode = true,
                            SupportedOperations = new[] { "Bypass FRP", "Format Userdata", "Read Info", "Reboot Reset" }
                        }
                    };

                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting Unisoc devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices connected via HUAWEI USB COM 1.0 Port (VID 0x12D1)
        /// </summary>
        public async Task<List<Device>> GetHuaweiPortsAsync()
        {
            var devices = new List<Device>();

            try
            {
                var ports = await Task.Run(() => HuaweiKirinEngine.ScanHuaweiPorts());
                foreach (var p in ports)
                {
                    var device = new Device
                    {
                        DeviceId = p.PortName,
                        SerialNumber = p.PortName,
                        Model = "Huawei HiSilicon Kirin (COM 1.0)",
                        Brand = "Huawei",
                        Manufacturer = "Huawei",
                        Mode = DeviceMode.Download,
                        Status = DeviceStatus.Connected,
                        LastSeen = DateTime.UtcNow,
                        Capabilities = new DeviceCapabilities
                        {
                            SupportsDownloadMode = true,
                            SupportedOperations = new[] { "Bypass Huawei ID", "Reset FRP", "Read Oeminfo", "Reboot Fastboot" }
                        }
                    };

                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting Huawei devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get devices connected via standard MTP / Windows USB PnP Bus (Zero USB Debugging required)
        /// </summary>
        public async Task<List<Device>> GetMtpPnpDevicesAsync()
        {
            var devices = new List<Device>();
            if (!OperatingSystem.IsWindows()) return devices;

            try
            {
                await Task.Run(() =>
                {
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        "SELECT Caption, Description, Name, DeviceID, PNPClass, Manufacturer, Service FROM Win32_PnPEntity WHERE " +
                        "PNPClass = 'WPD' OR " +
                        "PNPClass = 'AndroidUsbDeviceType' OR " +
                        "PNPClass = 'Portable Device' OR " +
                        "Service = 'WUDFWpdMtp' OR " +
                        "PNPDeviceID LIKE '%VID_0E8D%' OR " + // MediaTek MTP
                        "PNPDeviceID LIKE '%VID_05C6%' OR " + // Qualcomm MTP
                        "PNPDeviceID LIKE '%VID_22D9%' OR " + // OPPO / Realme
                        "PNPDeviceID LIKE '%VID_04E8%' OR " + // Samsung MTP
                        "PNPDeviceID LIKE '%VID_2717%' OR " + // Xiaomi MTP
                        "PNPDeviceID LIKE '%VID_2E04%' OR " + // Vivo MTP
                        "PNPDeviceID LIKE '%VID_12D1%' OR " + // Huawei MTP
                        "PNPDeviceID LIKE '%VID_18D1%' OR " + // Google MTP
                        "PNPDeviceID LIKE '%VID_1BBB%' OR " + // Transsion (Infinix / Tecno / Itel)
                        "PNPDeviceID LIKE '%VID_2A70%' OR " + // OnePlus
                        "PNPDeviceID LIKE '%VID_1782%'");     // Unisoc MTP

                    foreach (var obj in searcher.Get())
                    {
                        var deviceId = obj["DeviceID"]?.ToString() ?? "";
                        var caption = obj["Caption"]?.ToString() ?? obj["Name"]?.ToString() ?? "";
                        var mfg = obj["Manufacturer"]?.ToString() ?? "";

                        if (string.IsNullOrWhiteSpace(caption) ||
                            deviceId.Contains("ROOT_HUB", StringComparison.OrdinalIgnoreCase) || 
                            deviceId.Contains("Host Controller", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Hub", StringComparison.OrdinalIgnoreCase)) continue;

                        var serialParts = deviceId.Split('\\');
                        var serial = serialParts.Length >= 3 ? serialParts[2] : deviceId;

                        string brand = NormalizeBrand(mfg, caption);
                        if (brand == "Android Device" || brand == "Unknown")
                        {
                            if (deviceId.Contains("VID_22D9", StringComparison.OrdinalIgnoreCase)) brand = "Oppo";
                            else if (deviceId.Contains("VID_04E8", StringComparison.OrdinalIgnoreCase)) brand = "Samsung";
                            else if (deviceId.Contains("VID_2717", StringComparison.OrdinalIgnoreCase)) brand = "Xiaomi";
                            else if (deviceId.Contains("VID_2E04", StringComparison.OrdinalIgnoreCase)) brand = "Vivo";
                            else if (deviceId.Contains("VID_1BBB", StringComparison.OrdinalIgnoreCase)) brand = "Infinix";
                            else if (deviceId.Contains("VID_12D1", StringComparison.OrdinalIgnoreCase)) brand = "Huawei";
                        }

                        var (matchedProfile, _, _) = LoaderManager.ResolveHardwareIdentity(
                            brand,
                            caption,
                            "",
                            "",
                            caption,
                            caption);

                        string model = matchedProfile != null ? matchedProfile.ModelName : caption;
                        if (model.Equals("WinUsb Device", StringComparison.OrdinalIgnoreCase) || 
                            model.Equals("MTP Device", StringComparison.OrdinalIgnoreCase))
                        {
                            model = $"{brand} (Layar Terkunci / MTP)";
                        }

                        string chipset = matchedProfile?.Chipset ?? (!string.IsNullOrEmpty(brand) ? $"{brand} Hardware Platform" : "MTP Hardware Detected");
                        string softwareVer = "Android (Mode MTP / Layar Terkunci)";
                        string ram = "Profil Pabrik";
                        string storage = "Profil Pabrik";
                        string battery = "⚡ Mengisi Daya (USB)";
                        string secPatch = "Layar Terkunci (Pola/PIN Aktif)";
                        string resolution = "FHD+ Display";
                        string secStatus = "Lupa Pola / MTP";

                        var dev = new Device
                        {
                            DeviceId = serial,
                            SerialNumber = serial,
                            Model = model,
                            Brand = brand,
                            Manufacturer = brand,
                            SoftwareVersion = softwareVer,
                            Chipset = chipset,
                            TotalRam = ram,
                            StorageInfo = storage,
                            BatteryLevel = battery,
                            SecurityPatch = secPatch,
                            DisplayResolution = resolution,
                            SecurityStatus = secStatus,
                            Mode = DeviceMode.MTP,
                            Status = DeviceStatus.Connected,
                            LastSeen = DateTime.UtcNow,
                            Capabilities = new DeviceCapabilities
                            {
                                SupportsMtp = true,
                                SupportedOperations = new[] { "MTP Bypass FRP (Browser)", "Petunjuk Masuk Fastboot", "Petunjuk Masuk BROM / EDL" }
                            }
                        };

                        devices.Add(dev);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting MTP devices: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Get detailed device information
        /// </summary>
        private async Task<Device> GetDeviceInfoAsync(string deviceId)
        {
            var device = new Device
            {
                DeviceId = deviceId,
                SerialNumber = deviceId
            };

            try
            {
                // Get device properties
                var propsResult = await ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell getprop");
                var props = ParseDeviceProperties(propsResult);

                var rawModel = GetValue(props, "ro.product.model") ?? "Unknown";
                var rawBrand = GetValue(props, "ro.product.brand") ?? GetValue(props, "ro.product.manufacturer") ?? "Unknown";
                var marketName = GetValue(props, "ro.product.marketname") ?? GetValue(props, "ro.product.vendor.marketname") ?? GetValue(props, "ro.vendor.product.marketname") ?? "";

                device.Brand = NormalizeBrand(rawBrand, rawModel);
                device.Model = ResolveRealDeviceName(props, device.Brand, rawModel);
                device.Manufacturer = GetValue(props, "ro.product.manufacturer") ?? device.Brand;
                device.AndroidVersion = GetValue(props, "ro.build.version.release") ?? "Unknown";
                device.BuildNumber = GetValue(props, "ro.build.display.id") ?? "Unknown";
                device.SecurityPatch = GetValue(props, "ro.build.version.security_patch") ?? "-";
                device.Chipset = ResolveChipsetName(props);
                device.CpuInfo = device.Chipset;

                string displayId = GetValue(props, "ro.build.display.id") ?? "";
                device.SoftwareVersion = !string.IsNullOrEmpty(displayId) 
                    ? $"Android {device.AndroidVersion} ({displayId})" 
                    : $"Android {device.AndroidVersion}";

                // Run hardware detail queries in parallel
                try
                {
                    var memTask = ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell cat /proc/meminfo");
                    var dfTask = ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell \"df -h /storage/emulated/0 2>/dev/null || df -h /sdcard 2>/dev/null || df -h /data 2>/dev/null || cat /sys/block/sda/size 2>/dev/null || cat /sys/block/mmcblk0/size 2>/dev/null || df -h\"");
                    var battTask = ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell dumpsys battery");
                    var wmTask = ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell wm size");

                    await Task.WhenAll(memTask, dfTask, battTask, wmTask);

                    device.TotalRam = ParseRamInfo(await memTask);
                    device.StorageInfo = ParseStorageInfo(await dfTask);
                    device.BatteryLevel = ParseBatteryInfo(await battTask);
                    device.DisplayResolution = ParseDisplayResolution(await wmTask);
                }
                catch
                {
                    // Graceful fallback for non-standard ADB shells
                }

                // Check Bootloader / Security Status
                string ublProp = GetValue(props, "ro.boot.flash.locked") ?? GetValue(props, "ro.boot.verifiedbootstate") ?? "";
                device.SecurityStatus = (ublProp == "0" || ublProp.Equals("orange", StringComparison.OrdinalIgnoreCase) || ublProp.Equals("yellow", StringComparison.OrdinalIgnoreCase))
                    ? "UBL: Unlocked"
                    : "UBL: Locked";

                // Get IMEI (requires root or special permissions)
                try
                {
                    var imeiResult = await ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell service call iphonesubinfo 1");
                    device.Imei = ExtractImei(imeiResult);
                }
                catch
                {
                    device.Imei = "Not accessible";
                }

                // Check device capabilities
                device.Capabilities = await DetectDeviceCapabilitiesAsync(deviceId, props);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting device info for {deviceId}: {ex.Message}");
            }

            return device;
        }

        /// <summary>
        /// Get fastboot device information
        /// </summary>
        private async Task<Device> GetFastbootDeviceInfoAsync(string deviceId)
        {
            var device = new Device
            {
                DeviceId = deviceId,
                SerialNumber = deviceId,
                Mode = DeviceMode.Fastboot
            };

            try
            {
                var result = await ExecuteCommandAsync(_fastbootPath, $"-s {deviceId} getvar all");
                device.Model = ExtractFastbootVar(result, "product") ?? "Fastboot Device";
                device.Brand = ExtractFastbootVar(result, "product") ?? "Unknown";
                device.Capabilities = new DeviceCapabilities
                {
                    SupportsFastboot = true,
                    SupportsOdin = result.Contains("odin") || result.Contains("download"),
                    BootloaderUnlocked = result.Contains("unlocked:yes")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting fastboot device info for {deviceId}: {ex.Message}");
            }

            return device;
        }

        /// <summary>
        /// Detect device capabilities
        /// </summary>
        private async Task<DeviceCapabilities> DetectDeviceCapabilitiesAsync(string deviceId, Dictionary<string, string> props)
        {
            var capabilities = new DeviceCapabilities
            {
                SupportsAdb = true,
                SupportsFastboot = await CheckFastbootSupportAsync(deviceId),
                Rooted = props.ContainsKey("ro.build.selinux") && props["ro.build.selinux"] == "0"
            };

            // Brand-specific capabilities
            var brand = GetValue(props, "ro.product.brand")?.ToLower() ?? "";
            switch (brand)
            {
                case "samsung":
                    capabilities.SupportsOdin = true;
                    capabilities.SupportsDownloadMode = true;
                    break;
                case "xiaomi":
                    capabilities.SupportsEdl = true;
                    break;
                case "oppo":
                case "vivo":
                    capabilities.SupportsDownloadMode = true;
                    break;
            }

            return capabilities;
        }

        /// <summary>
        /// Check if device supports fastboot
        /// </summary>
        private async Task<bool> CheckFastbootSupportAsync(string deviceId)
        {
            try
            {
                var result = await ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell getprop ro.bootloader");
                return !string.IsNullOrEmpty(result.Trim());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// <summary>
        /// Execute ADB or fastboot command with strict timeout
        /// </summary>
        private async Task<string> ExecuteCommandAsync(string executable, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                throw new InvalidOperationException($"Failed to start process: {executable}");

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2000));
            try
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAny(process.WaitForExitAsync(cts.Token), Task.Delay(2000, cts.Token));

                if (!process.HasExited)
                {
                    try { process.Kill(true); } catch { }
                    return string.Empty;
                }

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    return string.Empty;
                }

                return output;
            }
            catch
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                return string.Empty;
            }
        }

        /// <summary>
        /// Parse device properties from getprop output
        /// </summary>
        private Dictionary<string, string> ParseDeviceProperties(string output)
        {
            var props = new Dictionary<string, string>();
            var regex = new Regex(@"\[(.*?)\]: \[(.*?)\]");

            foreach (Match match in regex.Matches(output))
            {
                if (match.Groups.Count == 3)
                {
                    props[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }

            return props;
        }

        /// <summary>
        /// Extract IMEI from service call output (handles both plain digits and Android Parcel UTF-16 dump with dots)
        /// </summary>
        public static string ExtractImei(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return "Not found";

            // If direct contiguous 15-digit number is found
            var directMatch = Regex.Match(output, @"\b([0-9]{15})\b");
            if (directMatch.Success) return directMatch.Groups[1].Value;

            // Parcel output format (UTF-16 with dots inside quotes: e.g. '.....8.6.1.4.3.0.0.5.1.2.3.4.5.6.7')
            var quoteMatches = Regex.Matches(output, @"'([^']*)'");
            if (quoteMatches.Count > 0)
            {
                var combined = string.Concat(quoteMatches.Select(m => m.Groups[1].Value));
                var digits = new string(combined.Where(char.IsDigit).ToArray());
                if (digits.Length >= 15) return digits.Substring(0, 15);
            }

            return "Not found";
        }

        /// <summary>
        /// Extract fastboot variable
        /// </summary>
        private string? ExtractFastbootVar(string output, string varName)
        {
            var regex = new Regex($@"{varName}:\s*(.+)");
            var match = regex.Match(output);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>
        /// Get value from properties dictionary
        /// </summary>
        private string? GetValue(System.Collections.Generic.Dictionary<string, string> props, string key)
        {
            return props.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Parse device status from ADB output
        /// </summary>
        private DeviceStatus ParseDeviceStatus(string status)
        {
            return status.ToLower() switch
            {
                "device" => DeviceStatus.Connected,
                "unauthorized" => DeviceStatus.Unauthorized,
                "offline" => DeviceStatus.Offline,
                _ => DeviceStatus.Error
            };
        }

        private string ParseRamInfo(string meminfo)
        {
            if (string.IsNullOrWhiteSpace(meminfo)) return "-";
            long totalKb = 0;
            long availKb = 0;

            var matchTotal = Regex.Match(meminfo, @"MemTotal:\s+(\d+)\s+kB", RegexOptions.IgnoreCase);
            if (matchTotal.Success) long.TryParse(matchTotal.Groups[1].Value, out totalKb);

            var matchAvail = Regex.Match(meminfo, @"MemAvailable:\s+(\d+)\s+kB", RegexOptions.IgnoreCase);
            if (matchAvail.Success) long.TryParse(matchAvail.Groups[1].Value, out availKb);

            if (totalKb <= 0) return "-";

            double totalGb = totalKb / 1048576.0;
            int roundedRamTier = totalGb switch
            {
                < 1.5 => 1,
                < 2.5 => 2,
                < 3.5 => 3,
                < 5.0 => 4,
                < 7.0 => 6,
                < 10.0 => 8,
                < 14.0 => 12,
                < 20.0 => 16,
                _ => (int)Math.Round(totalGb)
            };

            if (availKb > 0)
            {
                double availGb = availKb / 1048576.0;
                return $"{roundedRamTier} GB ({availGb:0.0} GB Bebas)";
            }

            return $"{roundedRamTier} GB";
        }

        private string ParseStorageInfo(string dfOutput)
        {
            if (string.IsNullOrWhiteSpace(dfOutput)) return "-";
            var lines = dfOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (line.Contains("/data", StringComparison.OrdinalIgnoreCase) || 
                    line.Contains("/storage", StringComparison.OrdinalIgnoreCase) || 
                    line.Contains("/sdcard", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("/mnt/", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("/dev/fuse", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("/dev/block/", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        string total = parts[1];
                        string avail = parts[3];

                        if (double.TryParse(Regex.Match(total, @"[\d\.]+").Value, out var num))
                        {
                            int tier = num switch
                            {
                                <= 16 => 16,
                                <= 32 => 32,
                                <= 64 => 64,
                                <= 128 => 128,
                                <= 256 => 256,
                                <= 512 => 512,
                                <= 1024 => 1024,
                                _ => (int)Math.Round(num)
                            };
                            return $"{tier} GB ({avail} Bebas)";
                        }

                        return $"{total} ({avail} Bebas)";
                    }
                }

                // Check raw hardware sector count (e.g. from /sys/block/sda/size or /sys/block/mmcblk0/size)
                if (long.TryParse(line.Trim(), out var sectors) && sectors > 1000000)
                {
                    long bytes = sectors * 512L;
                    double gb = bytes / 1000000000.0;
                    int tier = gb switch
                    {
                        <= 16 => 16,
                        <= 32 => 32,
                        <= 64 => 64,
                        <= 128 => 128,
                        <= 256 => 256,
                        <= 512 => 512,
                        <= 1024 => 1024,
                        _ => (int)Math.Round(gb)
                    };
                    return $"{tier} GB (Penyimpanan Internal)";
                }
            }
            return "-";
        }

        private string ParseBatteryInfo(string dumpsysBattery)
        {
            if (string.IsNullOrWhiteSpace(dumpsysBattery)) return "-";
            int level = -1;
            int status = -1;

            var matchLevel = Regex.Match(dumpsysBattery, @"level:\s*(\d+)", RegexOptions.IgnoreCase);
            if (matchLevel.Success) int.TryParse(matchLevel.Groups[1].Value, out level);

            var matchStatus = Regex.Match(dumpsysBattery, @"status:\s*(\d+)", RegexOptions.IgnoreCase);
            if (matchStatus.Success) int.TryParse(matchStatus.Groups[1].Value, out status);

            if (level < 0) return "-";

            string statusText = status switch
            {
                2 => "⚡ Mengisi Daya",
                3 => "Baterai",
                4 => "Tidak Mengisi",
                5 => "Penuh",
                _ => "Baterai"
            };

            return $"{level}% ({statusText})";
        }

        private string ParseDisplayResolution(string wmSize)
        {
            if (string.IsNullOrWhiteSpace(wmSize)) return "-";
            var match = Regex.Match(wmSize, @"Physical size:\s*(\d+x\d+)", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
            return "-";
        }

        private string ResolveChipsetName(Dictionary<string, string> props)
        {
            string platform = GetValue(props, "ro.board.platform") ?? "";
            string hardware = GetValue(props, "ro.hardware") ?? "";
            string soc = GetValue(props, "ro.soc.model") ?? "";
            string chipname = GetValue(props, "ro.chipname") ?? "";

            string raw = !string.IsNullOrEmpty(soc) ? soc : (!string.IsNullOrEmpty(platform) ? platform : (!string.IsNullOrEmpty(chipname) ? chipname : hardware));
            if (string.IsNullOrWhiteSpace(raw)) return "-";

            string lower = raw.ToLowerInvariant();
            if (lower.Contains("mt6765")) return "MediaTek Helio G35 / P35 (MT6765)";
            if (lower.Contains("mt6768")) return "MediaTek Helio G85 / G80 (MT6768)";
            if (lower.Contains("mt6762")) return "MediaTek Helio P22 (MT6762)";
            if (lower.Contains("mt6789") || lower.Contains("g99")) return "MediaTek Helio G99 (MT6789)";
            if (lower.Contains("mt6833") || lower.Contains("dimensity 700")) return "MediaTek Dimensity 700 / 6020";
            if (lower.Contains("sm6225") || lower.Contains("bengal")) return "Qualcomm Snapdragon 680 (SM6225)";
            if (lower.Contains("sm6125") || lower.Contains("trinket")) return "Qualcomm Snapdragon 665 (SM6125)";
            if (lower.Contains("sm7325") || lower.Contains("lahaina")) return "Qualcomm Snapdragon 778G (SM7325)";
            if (lower.Contains("sm8250") || lower.Contains("kona")) return "Qualcomm Snapdragon 865 (SM8250)";
            if (lower.Contains("sm8450") || lower.Contains("taro")) return "Qualcomm Snapdragon 8 Gen 1";
            if (lower.Contains("sm8550") || lower.Contains("kalama")) return "Qualcomm Snapdragon 8 Gen 2";
            if (lower.Contains("sdm660")) return "Qualcomm Snapdragon 660 (SDM660)";
            if (lower.Contains("ums512") || lower.Contains("t618") || lower.Contains("t616")) return "Unisoc Tiger T616 / T618";
            if (lower.Contains("ums9230") || lower.Contains("t606")) return "Unisoc Tiger T606";
            if (lower.Contains("sc9863")) return "Unisoc SC9863A";
            if (lower.Contains("exynos")) return $"Samsung Exynos ({raw})";

            return raw.ToUpperInvariant();
        }

        /// <summary>
        /// Get live battery status for telemetry
        /// </summary>
        public async Task<string> GetBatteryStatusLiveAsync(string deviceId)
        {
            try
            {
                var battTask = await ExecuteCommandAsync(_adbPath, $"-s {deviceId} shell dumpsys battery");
                return ParseBatteryInfo(battTask);
            }
            catch
            {
                return string.Empty;
            }
        }

        private string NormalizeBrand(string rawBrand, string rawModel)
        {
            if (string.IsNullOrWhiteSpace(rawBrand) || rawBrand.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                if (rawModel.StartsWith("CPH", StringComparison.OrdinalIgnoreCase)) return "OPPO";
                if (rawModel.StartsWith("RMX", StringComparison.OrdinalIgnoreCase)) return "Realme";
                if (rawModel.StartsWith("SM-", StringComparison.OrdinalIgnoreCase)) return "Samsung";
                if (rawModel.StartsWith("X6", StringComparison.OrdinalIgnoreCase)) return "Infinix";
                if (rawModel.StartsWith("V2", StringComparison.OrdinalIgnoreCase) || rawModel.StartsWith("PD", StringComparison.OrdinalIgnoreCase)) return "Vivo";
                return "Android";
            }

            var b = rawBrand.Trim();
            if (b.Equals("oppo", StringComparison.OrdinalIgnoreCase)) return "OPPO";
            if (b.Equals("samsung", StringComparison.OrdinalIgnoreCase)) return "Samsung";
            if (b.Equals("xiaomi", StringComparison.OrdinalIgnoreCase) || b.Equals("redmi", StringComparison.OrdinalIgnoreCase) || b.Equals("poco", StringComparison.OrdinalIgnoreCase)) return "Xiaomi";
            if (b.Equals("realme", StringComparison.OrdinalIgnoreCase)) return "Realme";
            if (b.Equals("vivo", StringComparison.OrdinalIgnoreCase)) return "Vivo";
            if (b.Equals("infinix", StringComparison.OrdinalIgnoreCase)) return "Infinix";
            if (b.Equals("tecno", StringComparison.OrdinalIgnoreCase)) return "Tecno";
            if (b.Equals("itel", StringComparison.OrdinalIgnoreCase)) return "Itel";
            if (b.Equals("huawei", StringComparison.OrdinalIgnoreCase) || b.Equals("honor", StringComparison.OrdinalIgnoreCase)) return "Huawei";

            return char.ToUpper(b[0]) + b.Substring(1);
        }

        private string ResolveRealDeviceName(Dictionary<string, string> props, string brand, string rawModel)
        {
            // 1. Baca kode identitas internal Android
            var prodName = GetValue(props, "ro.product.name") ?? "";
            var prodDevice = GetValue(props, "ro.product.device") ?? GetValue(props, "ro.build.product") ?? "";
            var boardPlatform = GetValue(props, "ro.board.platform") ?? GetValue(props, "ro.hardware") ?? "";
            var reportedMarketName = GetValue(props, "ro.vendor.oplus.market.name") ?? 
                                     GetValue(props, "ro.oppo.market.name") ?? 
                                     GetValue(props, "ro.product.marketname") ?? 
                                     GetValue(props, "ro.product.vendor.marketname") ?? 
                                     GetValue(props, "ro.vendor.product.marketname") ?? 
                                     GetValue(props, "ro.vivo.market.name") ?? 
                                     GetValue(props, "ro.transsion.market.name") ?? 
                                     GetValue(props, "ro.config.marketing_name") ?? "";

            // 2. UNIVERSAL FORENSIC RESOLVER (Berbasis Database Master Multi-Brand):
            // Mencocokkan SKU motherboard fisik terhadap database untuk membedakan HP Resmi Asli vs HP Rekondisi/KW
            var (_, isSpoofed, displayName) = LoaderManager.ResolveHardwareIdentity(
                brand,
                prodName,
                prodDevice,
                boardPlatform,
                rawModel,
                reportedMarketName);

            if (!string.IsNullOrEmpty(displayName) && displayName != rawModel)
            {
                return displayName;
            }

            // 3. Fallback: Baca descriptor Windows PnP USB
            var usbPnpName = QueryUsbBusReportedDescriptor(rawModel);
            if (!string.IsNullOrEmpty(usbPnpName) && !usbPnpName.Equals(rawModel, StringComparison.OrdinalIgnoreCase))
            {
                return $"{usbPnpName} ({rawModel})";
            }

            // 4. Fallback murni ke model asli
            return rawModel;
        }

        /// <summary>
        /// Membaca nama descriptor perangkat yang disiarkan langsung oleh kontroler USB ke Windows
        /// </summary>
        private static string? QueryUsbBusReportedDescriptor(string rawModel)
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return null;

                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Caption, Description, Name, DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'USB' OR PNPClass = 'Android' OR PNPClass = 'WPD' OR PNPClass = 'Ports'");
                
                foreach (var obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(caption) && 
                        !caption.Contains("Composite", StringComparison.OrdinalIgnoreCase) &&
                        !caption.Contains("Hub", StringComparison.OrdinalIgnoreCase) &&
                        !caption.Contains("Controller", StringComparison.OrdinalIgnoreCase) &&
                        !caption.Contains("Root", StringComparison.OrdinalIgnoreCase))
                    {
                        if (caption.Contains("OPPO", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Redmi", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Xiaomi", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Vivo", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Realme", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("Infinix", StringComparison.OrdinalIgnoreCase))
                        {
                            return caption.Replace("USB Device", "").Replace("Device", "").Trim();
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Restart ADB server
        /// </summary>
        public async Task RestartAdbServerAsync()
        {
            try
            {
                await ExecuteCommandAsync(_adbPath, "kill-server");
                await Task.Delay(1000);
                await ExecuteCommandAsync(_adbPath, "start-server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restarting ADB server: {ex.Message}");
            }
        }
    }
}
