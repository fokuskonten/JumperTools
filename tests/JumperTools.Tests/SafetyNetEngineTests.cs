using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using JumperTools.Core;
using JumperTools.Models;

namespace JumperTools.Tests
{
    public class SafetyNetEngineTests
    {
        [Fact]
        public void GetCriticalPartitions_ReturnsCorrectPartitionsForMtk()
        {
            var parts = SafetyNetEngine.GetCriticalPartitions("MediaTek");
            Assert.Contains("nvram", parts);
            Assert.Contains("nvdata", parts);
            Assert.Contains("persist", parts);
        }

        [Fact]
        public void GetCriticalPartitions_ReturnsCorrectPartitionsForQualcomm()
        {
            var parts = SafetyNetEngine.GetCriticalPartitions("Qualcomm");
            Assert.Contains("modemst1", parts);
            Assert.Contains("modemst2", parts);
            Assert.Contains("persist", parts);
        }

        [Fact]
        public async Task CreatePreFlightSnapshotAsync_CreatesFilesAndMetadata()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SafetyNetTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var engine = new SafetyNetEngine(tempDir);
                var snap = await engine.CreatePreFlightSnapshotAsync("Xiaomi", "Redmi Note 10", "SM7150", "TEST12345", "Qualcomm");

                Assert.NotNull(snap);
                Assert.True(Directory.Exists(snap.BackupDirectory));
                Assert.True(File.Exists(Path.Combine(snap.BackupDirectory, "snapshot_metadata.json")));
                Assert.True(snap.BackedUpPartitions.Count > 0);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }
    }
}
