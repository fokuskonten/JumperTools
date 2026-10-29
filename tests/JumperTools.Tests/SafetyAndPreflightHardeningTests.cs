using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Data;
using JumperTools.Models;
using JumperTools.Services;
using Xunit;

namespace JumperTools.Tests
{
    public class SafetyAndPreflightHardeningTests
    {
        private readonly string _tempDbPath;
        private readonly DatabaseContext _dbContext;
        private readonly JobJournalService _journalService;
        private readonly ToolRegistry _toolRegistry;
        private readonly PreflightEngine _preflightEngine;
        private readonly BackupIntegrityService _backupIntegrityService;
        private readonly OperationOrchestrator _orchestrator;

        public SafetyAndPreflightHardeningTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_safety_db_{Guid.NewGuid():N}.db");
            _dbContext = new DatabaseContext(_tempDbPath);
            _journalService = new JobJournalService(_dbContext);
            _toolRegistry = new ToolRegistry(Path.GetTempPath());
            _backupIntegrityService = new BackupIntegrityService(_dbContext);
            _preflightEngine = new PreflightEngine(_toolRegistry);
            _orchestrator = new OperationOrchestrator(_journalService, _preflightEngine, _backupIntegrityService, _toolRegistry);
        }

        [Fact]
        public async Task MandatoryBackupLock_FailedBackupOnSensitivePartition_ShouldAbortDestructiveOperation()
        {
            var device = new Device { DeviceId = "TEST_SENSITIVE_001", Brand = "Xiaomi", Model = "Redmi Note 9", Mode = DeviceMode.EDL };
            var executed = false;

            // FlashPartition on "nvram" is a sensitive partition requiring mandatory verified backup
            var result = await _orchestrator.RunOperationAsync(
                device,
                OperationType.FlashPartition,
                "Flash NVRAM Security Partition",
                executeAction: async (jobId, ct) =>
                {
                    executed = true;
                    await Task.Delay(10, ct);
                    return true;
                },
                backupAction: async (ct) =>
                {
                    await Task.Delay(5, ct);
                    return null; // Backup failed (e.g. read error)
                },
                targetPartition: "nvram");

            Assert.False(executed);
            Assert.Equal(OperationState.Failed, result.State);
            Assert.Equal(FailureClassification.VerificationFailed, result.FailureType);
            Assert.NotNull(result.FailureReason);
            Assert.Contains("Cadangan partisi vital", result.FailureReason);
        }

        [Fact]
        public async Task MandatoryBackupLock_CorruptZeroByteBackup_ShouldAbortDestructiveOperation()
        {
            var device = new Device { DeviceId = "TEST_SENSITIVE_002", Brand = "Samsung", Model = "Galaxy A30", Mode = DeviceMode.EDL };
            var executed = false;
            var tempZeroByteFile = Path.Combine(Path.GetTempPath(), $"zero_backup_{Guid.NewGuid():N}.bin");
            await File.WriteAllBytesAsync(tempZeroByteFile, Array.Empty<byte>());

            try
            {
                var result = await _orchestrator.RunOperationAsync(
                    device,
                    OperationType.FlashPartition,
                    "Flash EFS Security Block",
                    executeAction: async (jobId, ct) =>
                    {
                        executed = true;
                        await Task.CompletedTask;
                        return true;
                    },
                    backupAction: async (ct) =>
                    {
                        await Task.CompletedTask;
                        return tempZeroByteFile; // 0-byte unverified backup
                    },
                    targetPartition: "efs");

                Assert.False(executed);
                Assert.Equal(OperationState.Failed, result.State);
                Assert.Equal(FailureClassification.VerificationFailed, result.FailureType);
            }
            finally
            {
                if (File.Exists(tempZeroByteFile)) File.Delete(tempZeroByteFile);
            }
        }

        [Fact]
        public async Task ToolRegistry_BatchAudit_ShouldEvaluateAllTools()
        {
            var registry = new ToolRegistry(Path.GetTempPath());
            var auditResults = await registry.AuditAllToolsAsync();

            Assert.NotEmpty(auditResults);
            Assert.Contains("adb", auditResults.Keys);
            Assert.Contains("fastboot", auditResults.Keys);
            Assert.Contains("mtkclient", auditResults.Keys);
        }

        [Fact]
        public void FirmwareValidation_PackageInspection_ShouldHandleInvalidAndValidFiles()
        {
            var engine = new FirmwareCompatibilityEngine();

            var emptyPathResult = engine.ValidateFirmwarePackagePath("");
            Assert.Equal(PreflightLevel.Block, emptyPathResult.Level);

            var nonExistentResult = engine.ValidateFirmwarePackagePath("C:\\non_existent_fw_path.img");
            Assert.Equal(PreflightLevel.Block, nonExistentResult.Level);
        }

        [Fact]
        public async Task ProcessExecutionService_Cancellation_ShouldTerminateProcess()
        {
            var service = new ProcessExecutionService();
            var cmdExe = Path.Combine(Environment.SystemDirectory, "cmd.exe");

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(300); // Cancel after 300ms

            var result = await service.ExecuteAsync(cmdExe, "/c ping 127.0.0.1 -n 20", cancellationToken: cts.Token);

            Assert.False(result.Success);
            Assert.True(result.WasCancelled);
        }

        [Fact]
        public async Task JobJournal_QueryAuditFiltering_ShouldReturnMatchingRecords()
        {
            var jobId1 = _journalService.GenerateJobId();
            var jobId2 = _journalService.GenerateJobId();

            await _journalService.StartJobAsync(jobId1, "DEV_A", "Samsung", "A51", "Exynos", "COM1", "BypassFrp", operatorId: "tech_john");
            await _journalService.CompleteJobAsync(jobId1, true);

            await _journalService.StartJobAsync(jobId2, "DEV_B", "Xiaomi", "Redmi 9", "MTK", "COM2", "FlashPartition", operatorId: "tech_sarah");
            await _journalService.FailJobAsync(jobId2, FailureClassification.Disconnected, "Cable disconnected", "Check cable");

            var johnJobs = await _journalService.QueryAuditJournalAsync(operatorId: "tech_john");
            Assert.Contains(johnJobs, j => j.JobId == jobId1);
            Assert.DoesNotContain(johnJobs, j => j.JobId == jobId2);

            var failedJobs = await _journalService.QueryAuditJournalAsync(isSuccess: false);
            Assert.Contains(failedJobs, j => j.JobId == jobId2);
        }
    }
}
