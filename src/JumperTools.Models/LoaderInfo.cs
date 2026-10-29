using System;

namespace JumperTools.Models
{
    public enum LoaderType
    {
        Qualcomm_Firehose,
        MediaTek_DA,
        Unisoc_FDL,
        Kirin_Bootloader
    }

    public class LoaderInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public LoaderType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Sha256Checksum { get; set; } = string.Empty;
        public bool IsCachedLocally { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class PhoneModelProfile
    {
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string CodeName { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty; // Qualcomm / MTK / Unisoc / Exynos
        public string RecommendedLoaderFile { get; set; } = string.Empty;
        public string SupportedOperations { get; set; } = string.Empty;
    }
}
