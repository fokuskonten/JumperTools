using System;
using System.IO;
using System.Threading.Tasks;
using JumperTools.Core;
using JumperTools.Data;
using JumperTools.Models;
using JumperTools.Services;
using Xunit;

namespace JumperTools.Tests
{
    public class UsbAndFailureRecoveryTests
    {
        [Theory]
        [InlineData("Device disconnected unexpectedly", FailureClassification.Disconnected)]
        [InlineData("error 31: cannot open port COM5", FailureClassification.Disconnected)]
        [InlineData("USB device not recognized", FailureClassification.Disconnected)]
        [InlineData("device gone", FailureClassification.Disconnected)]
        [InlineData("Handshake failed: device in Preloader mode", FailureClassification.DeviceChanged)]
        [InlineData("Operation timed out waiting for Sahara Hello", FailureClassification.Timeout)]
        [InlineData("Access denied: Insufficient privileges to open USB endpoint", FailureClassification.PermissionDenied)]
        [InlineData("Signature verification failed: Anti-rollback index mismatch", FailureClassification.IncompatibleFirmware)]
        [InlineData("Checksum error on flashed partition", FailureClassification.VerificationFailed)]
        public void FailureClassification_Patterns_ShouldMapAccurately(string errorMessage, FailureClassification expected)
        {
            var result = UsbRecoveryService.ClassifyFailure(errorMessage, -1, false, false);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FailureClassification_ExplicitTimeoutAndCancel_ShouldTakePrecedence()
        {
            var timeoutRes = UsbRecoveryService.ClassifyFailure(null, -1, wasTimedOut: true, wasCancelled: false);
            Assert.Equal(FailureClassification.Timeout, timeoutRes);

            var cancelRes = UsbRecoveryService.ClassifyFailure("Some random error", -1, wasTimedOut: false, wasCancelled: true);
            Assert.Equal(FailureClassification.Cancelled, cancelRes);
        }

        [Fact]
        public void BlindRetryPolicy_DestructiveOperationFailedInExecuting_ShouldBeBlocked()
        {
            // FlashFirmware, FlashPartition, FormatUserdata, UnlockBootloader are destructive
            var isSafe = UsbRecoveryService.CanSafelyRetry(
                OperationType.FlashFirmware, 
                OperationState.Executing, 
                FailureClassification.Disconnected);

            Assert.False(isSafe);

            var isSafeUbl = UsbRecoveryService.CanSafelyRetry(
                OperationType.UnlockBootloader, 
                OperationState.Executing, 
                FailureClassification.Timeout);

            Assert.False(isSafeUbl);
        }

        [Fact]
        public void BlindRetryPolicy_FailedInPreflightOrBackup_ShouldBeAllowedToRetry()
        {
            var isSafe = UsbRecoveryService.CanSafelyRetry(
                OperationType.FlashFirmware, 
                OperationState.Backup, 
                FailureClassification.Disconnected);

            Assert.True(isSafe);
        }

        [Fact]
        public void RecoveryGuide_QualcommChipset_ShouldProvideTestPointProcedure()
        {
            var (summary, steps) = UsbRecoveryService.GetSafeRecoveryGuide(
                "Xiaomi", 
                "Snapdragon 720G (SM7125)", 
                OperationType.FlashPartition, 
                OperationState.Executing, 
                "boot");

            Assert.Contains("EDL 9008", summary);
            Assert.Contains(steps, s => s.Contains("Test Point"));
            Assert.Contains(steps, s => s.Contains("Firehose"));
        }

        [Fact]
        public void RecoveryGuide_MediaTekChipset_ShouldProvideBromKeyComboProcedure()
        {
            var (summary, steps) = UsbRecoveryService.GetSafeRecoveryGuide(
                "Oppo", 
                "Helio G85 (MT6769)", 
                OperationType.FactoryReset, 
                OperationState.Executing);

            Assert.Contains("BootROM", summary);
            Assert.Contains(steps, s => s.Contains("VOLUME ATAS + VOLUME BAWAH"));
            Assert.Contains(steps, s => s.Contains("UsbDk"));
        }

        [Fact]
        public void RecoveryGuide_SamsungChipset_ShouldProvideDownloadModeProcedure()
        {
            var (summary, steps) = UsbRecoveryService.GetSafeRecoveryGuide(
                "Samsung", 
                "Exynos 9611", 
                OperationType.FlashFirmware, 
                OperationState.Executing);

            Assert.Contains("Download Mode", summary);
            Assert.Contains(steps, s => s.Contains("Odin"));
        }

        [Fact]
        public void CrashRecovery_RiskClassification_ExecutingPhaseShouldBeHighRisk()
        {
            var record = new JobJournalRecord
            {
                JobId = "JOB-TEST-001",
                State = OperationState.Executing.ToString(),
                IsInterrupted = true,
                OperationType = OperationType.FlashPartition.ToString()
            };

            var (riskLevel, safeToRetry, recommendation) = JobJournalService.ClassifyInterruptedJobRisk(record);

            Assert.Equal("HIGH_RISK", riskLevel);
            Assert.False(safeToRetry);
            Assert.Contains("JANGAN BLIND RETRY", recommendation);
        }

        [Fact]
        public void CrashRecovery_RiskClassification_BackupPhaseShouldBeLowRisk()
        {
            var record = new JobJournalRecord
            {
                JobId = "JOB-TEST-002",
                State = OperationState.Backup.ToString(),
                IsInterrupted = true,
                OperationType = OperationType.FlashPartition.ToString()
            };

            var (riskLevel, safeToRetry, recommendation) = JobJournalService.ClassifyInterruptedJobRisk(record);

            Assert.Equal("LOW_RISK", riskLevel);
            Assert.True(safeToRetry);
            Assert.Contains("Aman untuk diulang", recommendation);
        }
    }
}
