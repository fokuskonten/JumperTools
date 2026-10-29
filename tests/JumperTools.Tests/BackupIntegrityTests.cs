using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JumperTools.Data;
using JumperTools.Services;
using Xunit;

namespace JumperTools.Tests
{
    public class BackupIntegrityTests
    {
        private readonly string _tempDbPath;
        private readonly DatabaseContext _dbContext;
        private readonly BackupIntegrityService _service;

        public BackupIntegrityTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_backup_db_{Guid.NewGuid():N}.db");
            _dbContext = new DatabaseContext(_tempDbPath);
            _service = new BackupIntegrityService(_dbContext);
        }

        [Fact]
        public async Task Backup_ValidFile_ShouldComputeCorrectSha256AndCreateManifest()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_dump_{Guid.NewGuid():N}.img");
            var content = "MOCK PARTITION DATA FOR NVRAM 0123456789";
            await File.WriteAllTextAsync(tempFile, content);

            try
            {
                var manifest = await _service.CreateAndVerifyManifestAsync(
                    "JOB-001",
                    "DEV-001",
                    "nvram",
                    tempFile,
                    "MediaTekBrom");

                Assert.True(manifest.IsVerified);
                Assert.NotEmpty(manifest.Sha256Hash);
                Assert.Equal(new FileInfo(tempFile).Length, manifest.FileSizeBytes);

                var sidecarPath = $"{tempFile}.manifest.json";
                Assert.True(File.Exists(sidecarPath));

                // Verify existing
                var isValid = await _service.VerifyExistingBackupAsync(tempFile);
                Assert.True(isValid);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                var sidecar = $"{tempFile}.manifest.json";
                if (File.Exists(sidecar)) File.Delete(sidecar);
            }
        }

        [Fact]
        public async Task Backup_ZeroByteFile_ShouldBeRejected()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_empty_{Guid.NewGuid():N}.img");
            await File.WriteAllBytesAsync(tempFile, Array.Empty<byte>());

            try
            {
                var manifest = await _service.CreateAndVerifyManifestAsync(
                    "JOB-002",
                    "DEV-002",
                    "modemst1",
                    tempFile,
                    "QualcommEdl",
                    expectedMinSizeBytes: 1024); // expects at least 1 KB

                Assert.False(manifest.IsVerified);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
