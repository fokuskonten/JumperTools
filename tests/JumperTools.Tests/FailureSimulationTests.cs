using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.Data;
using JumperTools.Services;

namespace JumperTools.Tests
{
    public class FailureSimulationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DatabaseContext _dbContext;
        private readonly ToolRegistry _toolRegistry;
        private readonly PreflightEngine _preflightEngine;
        private readonly JobJournalService _jobJournalService;
        private readonly BackupIntegrityService _backupService;
        private readonly OperationOrchestrator _orchestrator;
        private readonly ToolExecutionEngine _toolEngine;

        public FailureSimulationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_fail_db_{Guid.NewGuid()}.db");
            _dbContext = new DatabaseContext(_testDbPath);
            _toolRegistry = new ToolRegistry(AppDomain.CurrentDomain.BaseDirectory);
            _preflightEngine = new PreflightEngine(_toolRegistry);
            _jobJournalService = new JobJournalService(_dbContext);
            _backupService = new BackupIntegrityService(_dbContext);
            _orchestrator = new OperationOrchestrator(_jobJournalService, _preflightEngine, _backupService, _toolRegistry);
            _toolEngine = new ToolExecutionEngine(AppDomain.CurrentDomain.BaseDirectory, _toolRegistry);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }

        [Fact]
        public async Task Simulate_USBDisconnect_ShouldHandleCancellationSafely()
        {
            // Simulate a long running command that gets cancelled (like a cable disconnect)
            using var cts = new CancellationTokenSource();
            
            var runTask = _orchestrator.RunOperationAsync(
                new Device { DeviceId = "test", Brand = "Test", Model = "Test" }, 
                OperationType.FlashFirmware, 
                "Flash Firmware", 
                async (jobId, ct) =>
            {
                await Task.Delay(5000, ct); // Wait to simulate flash
                return true;
            }, externalCancellationToken: cts.Token);

            // Disconnect cable!
            cts.Cancel();

            var result = await runTask;
            Assert.True(result.State == OperationState.Failed || result.State == OperationState.Cancelled); // Must fail gracefully
        }

        [Fact]
        public async Task Simulate_MissingExecutable_ShouldBlockPreflight()
        {
            // Try to use a tool that is not installed or missing
            // This tests "missing executable" failure
            
            var runTask = await _orchestrator.RunOperationAsync(
                new Device { DeviceId = "test", Brand = "Test", Model = "Test" },
                OperationType.FlashFirmware, 
                "Test Ops", 
                async (jobId, ct) =>
                {
                    await Task.CompletedTask;
                    return true;
                }, requiredToolName: "missing_fake_tool.exe", externalCancellationToken: CancellationToken.None);

            Assert.Equal(OperationState.Failed, runTask.State); // Must fail
            // It should be handled by standard try/catch inside ExecuteToolAsync and returned to orchestrator
        }
        
        [Fact]
        public async Task Simulate_CorruptedBackup_ShouldBlockIntegrityService()
        {
            var jobId = Guid.NewGuid().ToString();
            // Create a fake backup
            var backupFile = Path.GetTempFileName();
            File.WriteAllText(backupFile, new string('A', 200)); // Make it large enough to pass expectedMinSizeBytes

            // Register it
            await _backupService.CreateAndVerifyManifestAsync(jobId, "test", "NVRAM", backupFile, "MTK", 100);

            // Corrupt it
            File.WriteAllText(backupFile, "corrupted dummy data");

            // Verify again
            var isIntact = await _backupService.VerifyExistingBackupAsync(backupFile);
            Assert.False(isIntact);
            
            File.Delete(backupFile);
        }

        [Fact]
        public async Task Simulate_ToolCrash_ShouldBeCaught()
        {
            var runTask = await _orchestrator.RunOperationAsync(
                new Device { DeviceId = "test", Brand = "Test", Model = "Test" },
                OperationType.FlashFirmware, 
                "Test Crash", 
                async (jobId, ct) =>
            {
                // Run a process that exits with non-zero (like cmd /c exit 1)
                var result = await _toolEngine.ProcessExecutor.ExecuteAsync("cmd.exe", "/c exit 1", "C:\\", 60000, ct);
                if (result.ExitCode != 0)
                    throw new Exception("Tool crashed with exit code 1");
                return true;
            }, externalCancellationToken: CancellationToken.None);

            Assert.Equal(OperationState.RecoveryRequired, runTask.State); // Destructive op crash -> RecoveryRequired
        }
    }
}
