using System;
using SQLite;

namespace JumperTools.Models
{
    public enum PinoutType
    {
        EDL_9008,
        MTK_BROM,
        SPREADTRUM_FDL,
        KIRIN_COM1,
        EXYNOS_EUB,
        ISP_EMMC,
        ISP_UFS
    }

    /// <summary>
    /// Represents a hardware Test Point or ISP Pinout diagram for a smartphone model
    /// </summary>
    public class TestPointInfo
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string MarketingName { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public PinoutType Type { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public string TestPointLocation { get; set; } = string.Empty;
        public string? RelativePath { get; set; }
        public string? ImageUrl { get; set; }
        public string? LocalImagePath { get; set; }
        public bool IsVerified { get; set; } = true;
    }
}
