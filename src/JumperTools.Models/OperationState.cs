using System;

namespace JumperTools.Models
{
    /// <summary>
    /// Deterministic lifecycle state machine for all hardware servicing operations
    /// </summary>
    public enum OperationState
    {
        Idle,
        Preflight,
        DeviceDetected,
        Backup,
        Preparing,
        Executing,
        Verifying,
        Completed,
        Failed,
        Cancelled,
        RecoveryRequired
    }

    /// <summary>
    /// Structured classification for operation failures
    /// </summary>
    public enum FailureClassification
    {
        None,
        Disconnected,
        Timeout,
        PermissionDenied,
        ToolMissing,
        ToolCorrupted,
        ToolUnsupportedVersion,
        DeviceChanged,
        VerificationFailed,
        Cancelled,
        IncompatibleFirmware,
        LowDiskSpace,
        UnknownFailure,
        RecoveryRequired
    }

    /// <summary>
    /// Structured result/progress update packet emitted by OperationOrchestrator
    /// </summary>
    public class OperationProgressUpdate
    {
        public string JobId { get; set; } = string.Empty;
        public OperationState State { get; set; } = OperationState.Idle;
        public double Percentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public string? CurrentPartition { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public FailureClassification FailureType { get; set; } = FailureClassification.None;
        public string? FailureReason { get; set; }
        public string? RecoveryAction { get; set; }
        public bool CanCancel { get; set; } = true;
        public bool IsRetryable { get; set; } = true;
        public string? InterruptedPhase { get; set; }
        public System.Collections.Generic.List<string> RecoverySteps { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Safety extensions for categorizing operation destructiveness and backup necessity
    /// </summary>
    public static class OperationTypeExtensions
    {
        public static bool IsDestructive(this OperationType type)
        {
            return type switch
            {
                OperationType.FlashFirmware => true,
                OperationType.FlashRecovery => true,
                OperationType.FlashCustomRom => true,
                OperationType.UnlockBootloader => true,
                OperationType.BypassFrp => true,
                OperationType.RemovePattern => true,
                OperationType.RemovePin => true,
                OperationType.RepairImei => true,
                OperationType.RepairBaseband => true,
                OperationType.RepairEfs => true,
                OperationType.FactoryReset => true,
                OperationType.FlashPartition => true,
                OperationType.ErasePartition => true,
                OperationType.WipeData => true,
                OperationType.OemUnlock => true,
                _ => false
            };
        }

        public static bool RequiresMandatoryBackup(this OperationType type, string? targetPartition = null)
        {
            if (type == OperationType.RepairImei || type == OperationType.RepairBaseband || type == OperationType.RepairEfs)
                return true;

            if (!string.IsNullOrEmpty(targetPartition))
            {
                var lower = targetPartition.ToLowerInvariant();
                if (lower.Contains("nvram") || lower.Contains("nvdata") || lower.Contains("efs") ||
                    lower.Contains("modemst") || lower.Contains("fsg") || lower.Contains("persist") ||
                    lower.Contains("oeminfo") || lower.Contains("seccfg") || lower.Contains("bootloader"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
