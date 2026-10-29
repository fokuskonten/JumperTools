using System;
using System.Collections.Generic;

namespace JumperTools.Models
{
    public enum FirmwarePackageType
    {
        MediaTekScatter,
        QualcommRawProgramXml,
        SamsungOdinTar,
        UnisocPac,
        FastbootScriptZip,
        Unknown
    }

    public class FirmwarePackageManifest
    {
        public string PackageName { get; set; } = string.Empty;
        public string PackageDirectory { get; set; } = string.Empty;
        public FirmwarePackageType PackageType { get; set; } = FirmwarePackageType.Unknown;
        public string TargetChipset { get; set; } = string.Empty;
        public List<PartitionTableItem> Items { get; set; } = new List<PartitionTableItem>();
        public long TotalPackageSizeBytes { get; set; }
        public string TotalPackageSizeFormatted => TotalPackageSizeBytes >= 1073741824
            ? $"{TotalPackageSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
            : $"{TotalPackageSizeBytes / (1024.0 * 1024.0):F1} MB";
        public bool IsValid { get; set; } = true;
        public string ValidationMessage { get; set; } = "Valid Manifest";
    }
}
