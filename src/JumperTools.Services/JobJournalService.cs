using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using JumperTools.Data;
using JumperTools.Models;

namespace JumperTools.Services
{
    /// <summary>
    /// Persistent journal service managing hardware job lifecycles and crash recovery tracking in SQLite
    /// </summary>
    public class JobJournalService
    {
        private readonly DatabaseContext? _dbContext;
        private readonly Dictionary<string, Stopwatch> _activeStopwatches = new();

        public JobJournalService(DatabaseContext? dbContext = null)
        {
            _dbContext = dbContext;
        }

        public string GenerateJobId()
        {
            return $"JOB-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        }

        public async Task<JobJournalRecord> StartJobAsync(
            string jobId,
            string deviceId,
            string brand,
            string model,
            string chipset,
            string portName,
            string operationType,
            string toolUsed = "",
            string targetPartition = "",
            string tenantId = "default",
            string operatorId = "local_admin")
        {
            var record = new JobJournalRecord
            {
                JobId = jobId,
                TenantId = tenantId,
                OperatorId = operatorId,
                DeviceId = deviceId,
                Brand = brand,
                Model = model,
                Chipset = chipset,
                PortName = portName,
                OperationType = operationType,
                ToolUsed = toolUsed,
                TargetPartition = targetPartition,
                State = OperationState.Preflight.ToString(),
                StartTime = DateTime.UtcNow,
                IsInterrupted = true, // Set to true while running so crashes are detectable
                IsSuccess = false,
                FailureClassification = FailureClassification.None.ToString()
            };

            _activeStopwatches[jobId] = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                try
                {
                    _dbContext?.SaveJobJournal(record);
                }
                catch { }
            });

            return record;
        }

        public async Task UpdateJobStateAsync(
            string jobId, 
            OperationState state, 
            string? stdoutAppend = null, 
            string? stderrAppend = null,
            string? backupPath = null,
            string? backupSha256 = null,
            bool backupVerified = false)
        {
            await Task.Run(() =>
            {
                try
                {
                    var record = _dbContext?.GetJobJournal(jobId);
                    if (record == null) return;

                    record.State = state.ToString();

                    if (!string.IsNullOrEmpty(stdoutAppend))
                    {
                        record.StdoutLog = string.IsNullOrEmpty(record.StdoutLog) 
                            ? stdoutAppend 
                            : $"{record.StdoutLog}\n{stdoutAppend}";
                    }

                    if (!string.IsNullOrEmpty(stderrAppend))
                    {
                        record.StderrLog = string.IsNullOrEmpty(record.StderrLog) 
                            ? stderrAppend 
                            : $"{record.StderrLog}\n{stderrAppend}";
                    }

                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        record.BackupPath = backupPath;
                        record.BackupSha256 = backupSha256;
                        record.BackupVerified = backupVerified;
                    }

                    _dbContext?.SaveJobJournal(record);
                }
                catch { }
            });
        }

        public async Task<JobJournalRecord?> CompleteJobAsync(
            string jobId, 
            bool success, 
            int exitCode = 0,
            string? finalStdout = null, 
            string? finalStderr = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var record = _dbContext?.GetJobJournal(jobId);
                    if (record == null) return null;

                    if (_activeStopwatches.TryGetValue(jobId, out var sw))
                    {
                        sw.Stop();
                        record.DurationMs = sw.ElapsedMilliseconds;
                        _activeStopwatches.Remove(jobId);
                    }

                    record.EndTime = DateTime.UtcNow;
                    record.IsInterrupted = false;
                    record.IsSuccess = success;
                    record.ExitCode = exitCode;
                    record.State = success ? OperationState.Completed.ToString() : OperationState.Failed.ToString();

                    if (!string.IsNullOrEmpty(finalStdout)) record.StdoutLog = finalStdout;
                    if (!string.IsNullOrEmpty(finalStderr)) record.StderrLog = finalStderr;

                    _dbContext?.SaveJobJournal(record);
                    return record;
                }
                catch
                {
                    return null;
                }
            });
        }

        public async Task<JobJournalRecord?> FailJobAsync(
            string jobId,
            FailureClassification failureType,
            string failureReason,
            string recoveryAction,
            int exitCode = -1,
            string? errorLog = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var record = _dbContext?.GetJobJournal(jobId);
                    if (record == null) return null;

                    if (_activeStopwatches.TryGetValue(jobId, out var sw))
                    {
                        sw.Stop();
                        record.DurationMs = sw.ElapsedMilliseconds;
                        _activeStopwatches.Remove(jobId);
                    }

                    record.EndTime = DateTime.UtcNow;
                    record.IsInterrupted = false;
                    record.IsSuccess = false;
                    record.ExitCode = exitCode;
                    record.State = OperationState.Failed.ToString();
                    record.FailureClassification = failureType.ToString();
                    record.FailureReason = failureReason;
                    record.RecoveryAction = recoveryAction;

                    if (!string.IsNullOrEmpty(errorLog))
                    {
                        record.StderrLog = errorLog;
                    }

                    _dbContext?.SaveJobJournal(record);
                    return record;
                }
                catch
                {
                    return null;
                }
            });
        }

        public async Task<List<JobJournalRecord>> DetectInterruptedJobsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return _dbContext?.GetInterruptedJobs() ?? new List<JobJournalRecord>();
                }
                catch
                {
                    return new List<JobJournalRecord>();
                }
            });
        }

        /// <summary>
        /// Analyzes an interrupted job's state to determine risk level and recovery readiness
        /// </summary>
        public static (string RiskLevel, bool IsSafeToAutoRetry, string Recommendation) ClassifyInterruptedJobRisk(JobJournalRecord job)
        {
            var state = job.State;
            if (state == OperationState.Preflight.ToString() || 
                state == OperationState.DeviceDetected.ToString() || 
                state == OperationState.Backup.ToString() || 
                state == OperationState.Preparing.ToString())
            {
                return ("LOW_RISK", true, "Aplikasi terhenti sebelum penulisan chip flash dimulai. Aman untuk diulang kembali.");
            }

            if (state == OperationState.Executing.ToString())
            {
                return ("HIGH_RISK", false, "Aplikasi terhenti SAAT PENULISAN HARDWARE FLASH MEMORY. JANGAN BLIND RETRY. Lakukan pengecekan fisik perangkat dan masuk mode bootloader/EDL.");
            }

            if (state == OperationState.Verifying.ToString())
            {
                return ("MEDIUM_RISK", false, "Penulisan selesai namun verifikasi belum tuntas. Dianjurkan melakukan verifikasi atau flash ulang dengan backup yang ada.");
            }

            return ("UNKNOWN_RISK", false, "Periksa log dan kondisi fisik perangkat sebelum melanjutkan.");
        }

        /// <summary>
        /// Query audit journal entries with filtering for compliance and service history
        /// </summary>
        public async Task<List<JobJournalRecord>> QueryAuditJournalAsync(
            string? operatorId = null,
            string? deviceId = null,
            string? operationType = null,
            bool? isSuccess = null,
            int limit = 100)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var all = _dbContext?.GetRecentJobJournals(limit * 2) ?? new List<JobJournalRecord>();
                    var filtered = all.AsEnumerable();

                    if (!string.IsNullOrEmpty(operatorId))
                        filtered = filtered.Where(j => j.OperatorId.Equals(operatorId, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(deviceId))
                        filtered = filtered.Where(j => j.DeviceId.Contains(deviceId, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(operationType))
                        filtered = filtered.Where(j => j.OperationType.Equals(operationType, StringComparison.OrdinalIgnoreCase));

                    if (isSuccess.HasValue)
                        filtered = filtered.Where(j => j.IsSuccess == isSuccess.Value);

                    return filtered.Take(limit).ToList();
                }
                catch
                {
                    return new List<JobJournalRecord>();
                }
            });
        }

        public async Task MarkJobAsRecoveredAsync(string jobId, string resolutionNotes)
        {
            await Task.Run(() =>
            {
                try
                {
                    var record = _dbContext?.GetJobJournal(jobId);
                    if (record == null) return;

                    record.IsInterrupted = false;
                    record.RecoveryAction = $"{record.RecoveryAction} | Resolved: {resolutionNotes}";
                    _dbContext?.SaveJobJournal(record);
                }
                catch { }
            });
        }
    }
}
