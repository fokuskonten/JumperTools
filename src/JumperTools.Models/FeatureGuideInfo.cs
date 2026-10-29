using System;
using System.Collections.Generic;

namespace JumperTools.Models
{
    public enum GuideRiskLevel
    {
        Safe,       // 🟢 Hijau: Read Info, Scan, Diag, Backup
        Warning,    // 🟡 Kuning: FRP Bypass, UBL, Sideload Wipe
        Critical    // 🔴 Merah: Format Userdata, Erase Partition, Flash Biner
    }

    public class FeatureGuideInfo
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = "ℹ️";
        public string Category { get; set; } = "Umum";
        public GuideRiskLevel RiskLevel { get; set; } = GuideRiskLevel.Safe;
        public string RiskDescription { get; set; } = "Aman / Non-Destruktif (Tidak menghapus data).";
        
        public string Description { get; set; } = string.Empty;
        public string WhenToUse { get; set; } = string.Empty;
        public List<string> Prerequisites { get; set; } = new();
        public List<string> StepByStepGuide { get; set; } = new();
        public string TargetChipsetOrBrand { get; set; } = "Semua Brand";
        public string Troubleshooting { get; set; } = string.Empty;

        public string RiskBadgeColor => RiskLevel switch
        {
            GuideRiskLevel.Safe => "#10B981",       // Green
            GuideRiskLevel.Warning => "#F59E0B",    // Amber / Yellow
            GuideRiskLevel.Critical => "#EF4444",   // Red
            _ => "#0284C7"
        };

        public string RiskBadgeText => RiskLevel switch
        {
            GuideRiskLevel.Safe => "🟢 TINGKAT RISIKO: AMAN (READ ONLY / NON-DESTRUKTIF)",
            GuideRiskLevel.Warning => "🟡 TINGKAT RISIKO: PERHATIAN (OPERASI BYPASS / SYSTEM)",
            GuideRiskLevel.Critical => "🔴 TINGKAT RISIKO: KRITIS (PENGHAPUSAN DATA / FLASH PARTISI)",
            _ => "🔵 INFORMASI FITUR"
        };
    }
}
