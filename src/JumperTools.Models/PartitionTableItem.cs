using System;

namespace JumperTools.Models
{
    public enum PartitionRiskLevel
    {
        Safe,
        Warning,
        CriticalRadio
    }

    public class PartitionTableItem
    {
        public int Index { get; set; }
        public string PartitionName { get; set; } = string.Empty;
        public string Name { get => PartitionName; set => PartitionName = value; }
        public string FileName { get; set; } = string.Empty;
        public long StartSector { get; set; }
        public long SectorCount { get; set; }
        public long SizeBytes { get; set; }
        public string SizeFormatted { get; set; } = "0 KB";
        public string Flags { get; set; } = string.Empty;
        public string? SelectedFileToFlash { get; set; }
        public bool IsSelected { get; set; } = true;
        public PartitionRiskLevel RiskLevel { get; set; } = PartitionRiskLevel.Safe;
        public string RiskBadgeColor => RiskLevel switch
        {
            PartitionRiskLevel.CriticalRadio => "#E74C3C",
            PartitionRiskLevel.Warning => "#F39C12",
            _ => "#2ECC71"
        };
        public string RiskDescription => RiskLevel switch
        {
            PartitionRiskLevel.CriticalRadio => "Sinyal / Baseband / IMEI (Kritis)",
            PartitionRiskLevel.Warning => "Sistem OS (Peringatan)",
            _ => "Aman (User / Data)"
        };
        public string Description => RiskDescription;
    }

    public enum DeviceConnectionProtocol
    {
        AutoDetect,
        Fastboot,
        QualcommEdl,
        MediaTekBrom,
        AdbRoot,
        SamsungOdin,
        UnisocSpd
    }

    public class PartitionItem : PartitionTableItem
    {
    }
}
