using System;
using SQLite;

namespace JumperTools.Models
{
    /// <summary>
    /// Represents a device operation (flashing, unlocking, etc.)
    /// </summary>
    public class Operation
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DeviceId { get; set; } = string.Empty;
        public OperationType Type { get; set; }
        public string ToolUsed { get; set; } = string.Empty;
        public OperationStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? ErrorMessage { get; set; }
        public int ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;

        [Ignore]
        public OperationParameters Parameters { get; set; } = new();
    }

    public enum OperationType
    {
        FlashFirmware,
        FlashRecovery,
        FlashCustomRom,
        UnlockBootloader,
        BypassFrp,
        RemovePattern,
        RemovePin,
        RepairImei,
        RepairBaseband,
        RepairEfs,
        FactoryReset,
        RootDevice,
        InstallMagisk,
        BackupPartition,
        RestorePartition,
        FlashPartition,
        ErasePartition,
        WipeData,
        OemUnlock
    }

    public enum OperationStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled,
        RequiresUserAction
    }

    public class OperationParameters
    {
        public string? FilePath { get; set; }
        public string? BlPath { get; set; }
        public string? ApPath { get; set; }
        public string? CpPath { get; set; }
        public string? CscPath { get; set; }
        public string? PitPath { get; set; }
        public string? RomVersion { get; set; }
        public bool KeepData { get; set; }
        public bool AutoReboot { get; set; }
        public string? CustomScript { get; set; }
        public System.Collections.Generic.Dictionary<string, string> AdditionalParams { get; set; } = new();
    }
}
