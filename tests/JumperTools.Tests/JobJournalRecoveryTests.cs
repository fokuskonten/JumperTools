using System;
using System.IO;
using System.Threading.Tasks;
using JumperTools.Data;
using JumperTools.Models;
using JumperTools.Services;
using Xunit;

namespace JumperTools.Tests
{
    public class JobJournalRecoveryTests
    {
        private readonly string _tempDbPath;
        private readonly DatabaseContext _dbContext;
        private readonly JobJournalService _service;

        public JobJournalRecoveryTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_journal_db_{Guid.NewGuid():N}.db");
            _dbContext = new DatabaseContext(_tempDbPath);
            _service = new JobJournalService(_dbContext);
        }

        [Fact]
        public async Task Journal_InterruptedJob_ShouldBeDetectedOnRestart()
        {
            var jobId = _service.GenerateJobId();

            // Simulate job started while app was running
            await _service.StartJobAsync(
                jobId,
                "DEV-12345",
                "Xiaomi",
                "Redmi 9",
                "MT6769",
                "COM5",
                "FormatUserdata");

            // App crashed suddenly (job was not completed/failed, so IsInterrupted is true)

            // App boots up and detects interrupted jobs
            var interruptedList = await _service.DetectInterruptedJobsAsync();

            Assert.NotEmpty(interruptedList);
            Assert.Contains(interruptedList, j => j.JobId == jobId && j.IsInterrupted);

            // Mark as recovered
            await _service.MarkJobAsRecoveredAsync(jobId, "Perangkat diverifikasi aman setelah restart.");

            var postRecoveryList = await _service.DetectInterruptedJobsAsync();
            Assert.DoesNotContain(postRecoveryList, j => j.JobId == jobId);
        }

        [Fact]
        public async Task Journal_CompletedJob_ShouldNotBeMarkedInterrupted()
        {
            var jobId = _service.GenerateJobId();

            await _service.StartJobAsync(
                jobId,
                "DEV-99999",
                "Samsung",
                "A50",
                "Exynos 9610",
                "COM3",
                "BypassFrp");

            await _service.CompleteJobAsync(jobId, true, 0, "Operation Completed successfully.");

            var record = _dbContext.GetJobJournal(jobId);
            Assert.NotNull(record);
            Assert.False(record.IsInterrupted);
            Assert.True(record.IsSuccess);
            Assert.Equal(OperationState.Completed.ToString(), record.State);
        }
    }
}
