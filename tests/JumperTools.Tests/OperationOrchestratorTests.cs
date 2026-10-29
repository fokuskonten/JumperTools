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
    public class OperationOrchestratorTests
    {
        private readonly string _tempDbPath;
        private readonly DatabaseContext _dbContext;
        private readonly JobJournalService _journalService;
        private readonly ToolRegistry _toolRegistry;
        private readonly PreflightEngine _preflightEngine;
        private readonly BackupIntegrityService _backupIntegrityService;
        private readonly OperationOrchestrator _orchestrator;

        public OperationOrchestratorTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.db");
            _dbContext = new DatabaseContext(_tempDbPath);
            _journalService = new JobJournalService(_dbContext);
            _toolRegistry = new ToolRegistry(Path.GetTempPath());
            _preflightEngine = new PreflightEngine(_toolRegistry);
            _backupIntegrityService = new BackupIntegrityService(_dbContext);
            _orchestrator = new OperationOrchestrator(_journalService, _preflightEngine, _backupIntegrityService, _toolRegistry);
        }

        [Fact]
        public async Task Orchestrator_SuccessfulLifecycle_ShouldReachCompleted()
        {
            var device = new Device { DeviceId = "TEST_SERIAL_001", Brand = "Samsung", Model = "Galaxy A51", Mode = DeviceMode.Normal };
            var statesObserved = new List<OperationState>();

            _orchestrator.ProgressChanged += (up) =>
            {
                if (!statesObserved.Contains(up.State)) statesObserved.Add(up.State);
            };

            var result = await _orchestrator.RunOperationAsync(
                device,
                OperationType.BypassFrp,
                "Bypass FRP Google Account",
                executeAction: async (jobId, ct) =>
                {
                    await Task.Delay(10, ct);
                    return true;
                },
                verifyAction: async (ct) =>
                {
                    await Task.Delay(5, ct);
                    return true;
                });

            Assert.Equal(OperationState.Completed, result.State);
            Assert.Equal(100, result.Percentage);
            Assert.Equal(FailureClassification.None, result.FailureType);
            Assert.Contains(OperationState.Preflight, statesObserved);
            Assert.Contains(OperationState.DeviceDetected, statesObserved);
            Assert.Contains(OperationState.Preparing, statesObserved);
            Assert.Contains(OperationState.Executing, statesObserved);
            Assert.Contains(OperationState.Verifying, statesObserved);
            Assert.Contains(OperationState.Completed, statesObserved);
        }

        [Fact]
        public async Task Orchestrator_PreflightBlock_ShouldStopImmediately()
        {
            var device = new Device { DeviceId = "TEST_DEV_002", Brand = "Xiaomi", Model = "Redmi Note 10", Mode = DeviceMode.Fastboot };
            var capability = new DeviceCapabilityProfile
            {
                Brand = "Xiaomi",
                Model = "Redmi Note 10",
                RestrictedOperations = new List<OperationType> { OperationType.RepairImei }
            };

            var executed = false;
            var result = await _orchestrator.RunOperationAsync(
                device,
                OperationType.RepairImei,
                "Repair IMEI",
                executeAction: async (jobId, ct) =>
                {
                    executed = true;
                    await Task.CompletedTask;
                    return true;
                },
                capabilityProfile: capability);

            Assert.False(executed);
            Assert.Equal(OperationState.Failed, result.State);
            Assert.Equal(FailureClassification.PermissionDenied, result.FailureType);
            Assert.NotNull(result.FailureReason);
            Assert.Contains("dibatasi atau tidak didukung", result.FailureReason);
        }

        [Fact]
        public async Task Orchestrator_ExecutionFailure_ShouldClassifyAndRecordFailure()
        {
            var device = new Device { DeviceId = "TEST_DEV_003", Brand = "Qualcomm", Model = "Generic 9008", Mode = DeviceMode.EDL };

            var result = await _orchestrator.RunOperationAsync(
                device,
                OperationType.FlashPartition,
                "Flash Raw Image",
                executeAction: async (jobId, ct) =>
                {
                    await Task.Delay(10, ct);
                    return false; // Hardware write failed
                });

            Assert.Equal(OperationState.RecoveryRequired, result.State);
            Assert.False(result.IsRetryable);
            Assert.Equal(FailureClassification.UnknownFailure, result.FailureType);
            Assert.NotNull(result.FailureReason);
            Assert.NotNull(result.RecoveryAction);
            Assert.NotEmpty(result.RecoverySteps);
        }

        [Fact]
        public async Task Orchestrator_ManualCancellation_ShouldStopSafely()
        {
            var device = new Device { DeviceId = "TEST_DEV_004", Brand = "Samsung", Model = "Galaxy S20", Mode = DeviceMode.Download };
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel before execution

            var executed = false;
            var result = await _orchestrator.RunOperationAsync(
                device,
                OperationType.FlashFirmware,
                "Flash 4-File Odin",
                executeAction: async (jobId, ct) =>
                {
                    executed = true;
                    await Task.CompletedTask;
                    return true;
                },
                externalCancellationToken: cts.Token);

            Assert.False(executed);
            Assert.Equal(OperationState.Cancelled, result.State);
            Assert.Equal(FailureClassification.Cancelled, result.FailureType);
        }
    }
}
