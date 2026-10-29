using System;
using System.Collections.Generic;

namespace JumperTools.Models
{
    /// <summary>
    /// Detailed metadata descriptor for a firmware archive or single partition image
    /// </summary>
    public class FirmwarePackageMetadata
    {
        public string Brand { get; set; } = string.Empty;
        public string TargetModel { get; set; } = string.Empty;
        public string TargetVariant { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string SocPlatform { get; set; } = string.Empty;
        public string BuildVersion { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public string SecurityPatchLevel { get; set; } = string.Empty;
        public string PackageFilePath { get; set; } = string.Empty;
        public long PackageSizeBytes { get; set; }
        public string PackageSha256 { get; set; } = string.Empty;
        public List<string> IncludedPartitions { get; set; } = new();
    }
}
