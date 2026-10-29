using System;
using System.Collections.Generic;

namespace JumperTools.Models
{
    /// <summary>
    /// Unified capability profile defining hardware limits, allowed operations, and required tools per device/SoC
    /// </summary>
    public class DeviceCapabilityProfile
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string SocPlatform { get; set; } = string.Empty; // Qualcomm, MediaTek, Spreadtrum, Exynos, Kirin, Apple
        public string StorageType { get; set; } = "eMMC"; // eMMC, UFS, NAND, NVMe
        public string PartitionScheme { get; set; } = "GPT"; // GPT, MBR, Scatter, RAW

        public List<DeviceMode> SupportedBootModes { get; set; } = new();
        public List<OperationType> SupportedOperations { get; set; } = new();
        public List<OperationType> RestrictedOperations { get; set; } = new();
        public List<string> RequiredTools { get; set; } = new();
        public List<string> RequiredDrivers { get; set; } = new();
        public bool RequiresAuthToken { get; set; }
        public bool RequiresTestPointForBypass { get; set; }
        public long MinFreeDiskSpaceBytes { get; set; } = 1073741824; // 1 GB default

        public bool IsOperationAllowed(OperationType operation)
        {
            if (RestrictedOperations.Contains(operation)) return false;
            if (SupportedOperations.Count == 0) return true; // Default permissive if not explicitly filtered
            return SupportedOperations.Contains(operation);
        }
    }

    public enum PreflightLevel
    {
        Pass,
        Warning,
        Block
    }

    public class PreflightEvaluation
    {
        public PreflightLevel Level { get; set; } = PreflightLevel.Pass;
        public bool IsBlocked => Level == PreflightLevel.Block;
        public List<string> Messages { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> BlockingReasons { get; set; } = new();
        public string? RecommendedFix { get; set; }
    }
}
