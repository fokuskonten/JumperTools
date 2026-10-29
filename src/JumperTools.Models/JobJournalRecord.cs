using System;
using SQLite;

namespace JumperTools.Models
{
    /// <summary>
    /// Persistent journal record representing a single hardware servicing job, surviving application restarts
    /// </summary>
    [Table("job_journal_entries")]
    public class JobJournalRecord
    {
        [PrimaryKey]
        public string JobId { get; set; } = string.Empty;

        [Indexed]
        public string TenantId { get; set; } = string.Empty;

        public string OperatorId { get; set; } = string.Empty;

        [Indexed]
        public string DeviceId { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string PortName { get; set; } = string.Empty;

        public string OperationType { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty; // OperationState string representation

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public long DurationMs { get; set; }

        public string ToolUsed { get; set; } = string.Empty;
        public string ToolVersion { get; set; } = string.Empty;
        public string ToolExecutablePath { get; set; } = string.Empty;

        public string? FirmwarePath { get; set; }
        public string? FirmwareSha256 { get; set; }

        public string? TargetPartition { get; set; }
        public string? BackupPath { get; set; }
        public string? BackupSha256 { get; set; }
        public bool BackupVerified { get; set; }

        public string? CommandMetadata { get; set; }
        public string? StdoutLog { get; set; }
        public string? StderrLog { get; set; }
        public int ExitCode { get; set; }

        public string FailureClassification { get; set; } = "None";
        public string? FailureReason { get; set; }
        public string? RecoveryAction { get; set; }

        [Indexed]
        public bool IsInterrupted { get; set; } // True if application was closed/crashed while job was in-progress

        public bool IsSuccess { get; set; }
    }

    /// <summary>
    /// Manifest metadata for verified partition dumps
    /// </summary>
    [Table("backup_manifests")]
    public class BackupManifestRecord
    {
        [PrimaryKey]
        public string ManifestId { get; set; } = Guid.NewGuid().ToString();

        [Indexed]
        public string JobId { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;
        public string PartitionName { get; set; } = string.Empty;
        public string BackupFilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string ProtocolUsed { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
}
