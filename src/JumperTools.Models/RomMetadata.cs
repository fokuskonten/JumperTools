using System;
using SQLite;

namespace JumperTools.Models
{
    /// <summary>
    /// Represents ROM/firmware metadata from server
    /// </summary>
    public class RomMetadata
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Checksum { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;

        [Ignore]
        public System.Collections.Generic.Dictionary<string, string> MirrorUrls { get; set; } = new();

        public DateTime UploadDate { get; set; }
        public string? CompatibilityNotes { get; set; }
        public bool IsOfficial { get; set; }
        public RomType Type { get; set; }

        [Ignore]
        public string[] SupportedModels { get; set; } = Array.Empty<string>();
    }

    public enum RomType
    {
        Stock,
        Custom,
        Recovery,
        Kernel,
        Modem,
        Bootloader
    }
}
