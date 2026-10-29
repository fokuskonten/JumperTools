using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using JumperTools.Data;
using JumperTools.Models;

namespace JumperTools.Services
{
    /// <summary>
    /// Service for validating dump integrity, computing SHA-256 hashes, detecting zero-entropy dumps, and generating manifest metadata
    /// </summary>
    public class BackupIntegrityService
    {
        private readonly DatabaseContext? _dbContext;

        public BackupIntegrityService(DatabaseContext? dbContext = null)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Compute SHA-256 hash of a file
        /// </summary>
        public async Task<string> ComputeFileSha256Async(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Returns expected minimum size in bytes for a given partition type
        /// </summary>
        public static long GetMinimumExpectedSizeForPartition(string partitionName)
        {
            var lower = partitionName.ToLowerInvariant();
            if (lower.Contains("nvram") || lower.Contains("nvdata") || lower.Contains("efs") ||
                lower.Contains("modemst") || lower.Contains("fsg") || lower.Contains("persist"))
            {
                return 512; // Minimum 512 bytes for security blocks
            }
            if (lower.Contains("boot") || lower.Contains("recovery") || lower.Contains("vbmeta"))
            {
                return 4096; // Minimum 4 KB
            }
            if (lower.Contains("system") || lower.Contains("super") || lower.Contains("userdata"))
            {
                return 1048576; // Minimum 1 MB
            }
            return 512;
        }

        /// <summary>
        /// Validates that a dumped file is not 100% empty (all 0x00 or all 0xFF, which indicates uninitialized/failed hardware read)
        /// </summary>
        public async Task<bool> IsValidNonBlankDumpAsync(string filePath, int sampleSize = 4096)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var length = stream.Length;
                if (length == 0) return false;

                var readSize = (int)Math.Min(sampleSize, length);
                var buffer = new byte[readSize];
                var read = await stream.ReadAsync(buffer, 0, readSize);
                if (read == 0) return false;

                // Check if not all 0x00 and not all 0xFF
                bool hasNonZero = false;
                bool hasNonFF = false;
                for (int i = 0; i < read; i++)
                {
                    if (buffer[i] != 0x00) hasNonZero = true;
                    if (buffer[i] != 0xFF) hasNonFF = true;
                    if (hasNonZero && hasNonFF) return true;
                }

                // If sample was uniform, check the middle and end of file if large enough
                if (length > readSize * 2)
                {
                    stream.Seek(length / 2, SeekOrigin.Begin);
                    read = await stream.ReadAsync(buffer, 0, readSize);
                    for (int i = 0; i < read; i++)
                    {
                        if (buffer[i] != 0x00) hasNonZero = true;
                        if (buffer[i] != 0xFF) hasNonFF = true;
                        if (hasNonZero && hasNonFF) return true;
                    }
                }

                return hasNonZero && hasNonFF;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates backup file existence, verifies non-zero size, computes SHA-256, writes manifest JSON sidecar, and persists to DB
        /// </summary>
        public async Task<BackupManifestRecord> CreateAndVerifyManifestAsync(
            string jobId, 
            string deviceId, 
            string partitionName, 
            string backupFilePath, 
            string protocolUsed,
            long expectedMinSizeBytes = 1)
        {
            var manifest = new BackupManifestRecord
            {
                JobId = jobId,
                DeviceId = deviceId,
                PartitionName = partitionName,
                BackupFilePath = backupFilePath,
                ProtocolUsed = protocolUsed,
                CreatedAt = DateTime.UtcNow,
                IsVerified = false
            };

            if (!File.Exists(backupFilePath))
            {
                return manifest;
            }

            var fileInfo = new FileInfo(backupFilePath);
            manifest.FileSizeBytes = fileInfo.Length;

            if (fileInfo.Length < expectedMinSizeBytes)
            {
                return manifest;
            }

            // Verify not all 0x00 / 0xFF blank dump
            var isNotBlank = await IsValidNonBlankDumpAsync(backupFilePath);
            if (!isNotBlank && fileInfo.Length <= 4096)
            {
                // Small files that are all zero or all FF are treated as unverified
                manifest.IsVerified = false;
                return manifest;
            }

            // Compute hash
            manifest.Sha256Hash = await ComputeFileSha256Async(backupFilePath);
            manifest.IsVerified = !string.IsNullOrEmpty(manifest.Sha256Hash) && manifest.FileSizeBytes >= expectedMinSizeBytes;

            // Write JSON manifest sidecar file (.manifest.json)
            var manifestFilePath = $"{backupFilePath}.manifest.json";
            try
            {
                var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                await File.WriteAllTextAsync(manifestFilePath, json);
            }
            catch { }

            // Save to database
            try
            {
                _dbContext?.SaveBackupManifest(manifest);
            }
            catch { }

            return manifest;
        }

        /// <summary>
        /// Verify an existing backup file against its manifest sidecar or hash
        /// </summary>
        public async Task<bool> VerifyExistingBackupAsync(string backupFilePath)
        {
            if (!File.Exists(backupFilePath)) return false;

            var manifestPath = $"{backupFilePath}.manifest.json";
            if (!File.Exists(manifestPath))
            {
                // Fallback size check
                return new FileInfo(backupFilePath).Length > 0;
            }

            try
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonConvert.DeserializeObject<BackupManifestRecord>(json);
                if (manifest == null) return false;

                var currentHash = await ComputeFileSha256Async(backupFilePath);
                return string.Equals(currentHash, manifest.Sha256Hash, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
