using System;
using SQLite;

namespace JumperTools.Models
{
    /// <summary>
    /// Represents a connected Android device
    /// </summary>
    public class Device
    {
        [PrimaryKey]
        public string DeviceId { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public string BuildNumber { get; set; } = string.Empty;
        public string Imei { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public string PortName { get; set; } = string.Empty;
        public string TotalRam { get; set; } = "-";
        public string StorageInfo { get; set; } = "-";
        public string BatteryLevel { get; set; } = "-";
        public string SecurityPatch { get; set; } = "-";
        public string DisplayResolution { get; set; } = "-";
        public string SoftwareVersion { get; set; } = "-";
        public string CpuInfo { get; set; } = "-";
        public string SecurityStatus { get; set; } = "-";
        public DeviceMode Mode { get; set; }
        public DeviceStatus Status { get; set; }
        public DateTime LastSeen { get; set; }
        public string? Fingerprint { get; set; }

        [Ignore]
        public DeviceCapabilities Capabilities { get; set; } = new();
    }

    public enum DeviceMode
    {
        Normal,
        Recovery,
        Fastboot,
        Download,
        EDL,
        Odin,
        BROM,     // MediaTek Boot ROM mode (mtkclient / SP Flash Tool)
        MTP,      // Windows USB PnP / MTP mode (Setup Wizard / Lockscreen / Zero ADB)
        Unknown
    }

    public enum DeviceStatus
    {
        Connected,
        Disconnected,
        Unauthorized,
        Offline,
        Error
    }

    public class DeviceCapabilities
    {
        public bool SupportsAdb { get; set; }
        public bool SupportsFastboot { get; set; }
        public bool SupportsOdin { get; set; }
        public bool SupportsEdl { get; set; }
        public bool SupportsDownloadMode { get; set; }
        public bool SupportsRecovery { get; set; }
        public bool SupportsMtp { get; set; }
        public bool BootloaderUnlocked { get; set; }
        public bool Rooted { get; set; }
        public string[] SupportedOperations { get; set; } = Array.Empty<string>();
    }
}
