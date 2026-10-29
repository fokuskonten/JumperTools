using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;

namespace JumperTools.Core
{
    public class DriverInfoItem
    {
        public string DriverName { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
        public string PortOrDeviceName { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public bool HasConflict { get; set; }
        public string? ConflictReason { get; set; }
        public string? RepairGuidance { get; set; }
    }

    /// <summary>
    /// Intelligent driver diagnostics, deterministic USB HWID mapping, and conflict resolution service
    /// </summary>
    public class DriverIntelligenceService
    {
        public List<DriverInfoItem> ScanDrivers()
        {
            var results = new List<DriverInfoItem>();

            var driverProfiles = new List<(string Name, string Pattern, string Protocol, string Interface)>
            {
                ("Qualcomm QDLoader 9008 Driver", @"USB\\VID_05C6&PID_9008", "Qualcomm EDL 9008", "Emergency Download Interface"),
                ("Qualcomm HS-USB Diagnostics", @"USB\\VID_05C6&PID_(900E|9091|901D|9025|903F)", "Qualcomm Diag", "Baseband/QCN Calibration Port"),
                ("MediaTek USB VCOM (BootROM)", @"USB\\VID_0E8D&PID_0003", "MediaTek BROM", "MTK Low-Level BROM Port"),
                ("MediaTek DA USB VCOM (Preloader)", @"USB\\VID_0E8D&PID_2000", "MediaTek Preloader", "MTK Flashing Port"),
                ("Spreadtrum / Unisoc BSL Driver", @"USB\\VID_1782&PID_4D00", "Unisoc BSL", "Spreadtrum ROM Download Port"),
                ("Samsung Mobile USB Modem", @"USB\\VID_04E8", "Samsung AT / Odin", "Samsung Modem & Download Port"),
                ("Google Android ADB Interface", @"USB\\VID_18D1&PID_(4EE7|4EE2|D002)", "Android ADB", "ADB Shell / Sideload"),
                ("Android Bootloader Interface", @"USB\\VID_18D1&PID_4EE0", "Fastboot", "Fastboot Protocol"),
                ("Apple Mobile Device USB Driver", @"USB\\VID_05AC", "Apple iOS", "DFU / Recovery / Normal iOS")
            };

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPDeviceID IS NOT NULL");
                var entities = searcher.Get();

                foreach (var profile in driverProfiles)
                {
                    var item = new DriverInfoItem
                    {
                        DriverName = profile.Name,
                        Protocol = profile.Protocol,
                        InterfaceType = profile.Interface
                    };

                    foreach (var obj in entities)
                    {
                        var pnpId = obj["PNPDeviceID"]?.ToString() ?? string.Empty;
                        var caption = obj["Caption"]?.ToString() ?? string.Empty;

                        if (Regex.IsMatch(pnpId, profile.Pattern, RegexOptions.IgnoreCase))
                        {
                            item.IsInstalled = true;
                            item.HardwareId = pnpId;
                            item.PortOrDeviceName = caption;

                            // Check for code 10/28/39 driver errors
                            var configErrorCode = obj["ConfigManagerErrorCode"] != null ? Convert.ToInt32(obj["ConfigManagerErrorCode"]) : 0;
                            if (configErrorCode != 0)
                            {
                                item.HasConflict = true;
                                item.ConflictReason = $"Driver mengalami error Windows Code {configErrorCode} (Driver rusak atau tanda tangan digital ditolak).";
                                item.RepairGuidance = "Pasang ulang driver resmi dengan mematikan Driver Signature Enforcement atau gunakan Zadig/libusb.";
                            }

                            break;
                        }
                    }

                    if (!item.IsInstalled)
                    {
                        item.RepairGuidance = $"Driver belum terdeteksi aktif. Silakan pasang paket driver {profile.Name}.";
                    }

                    results.Add(item);
                }
            }
            catch (Exception ex)
            {
                results.Add(new DriverInfoItem
                {
                    DriverName = "WMI Scan Diagnostics",
                    HasConflict = true,
                    ConflictReason = $"Gagal memindai driver WMI: {ex.Message}"
                });
            }

            return results;
        }
    }
}
