using System;
using System.Collections.Generic;

namespace JumperTools.Models
{
    public class SafetySnapshotInfo
    {
        public string SnapshotId { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string Brand { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Chipset { get; set; } = string.Empty;
        public string SerialNo { get; set; } = "UNKNOWN";
        public string BackupDirectory { get; set; } = string.Empty;
        public List<string> BackedUpPartitions { get; set; } = new List<string>();
        public string PartitionsSummary => string.Join(", ", BackedUpPartitions);
        public long TotalSizeBytes { get; set; }
        public string TotalSizeFormatted => TotalSizeBytes >= 1048576 
            ? $"{TotalSizeBytes / (1024.0 * 1024.0):F1} MB" 
            : $"{TotalSizeBytes / 1024.0:F0} KB";
        public string Status { get; set; } = "Verified (Valid)";
    }
}
