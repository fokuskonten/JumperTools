using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;
using JumperTools.Services;

namespace JumperTools.Core
{
    /// <summary>
    /// Centralized state machine & orchestrator for all hardware operations with deterministic lifecycle, failure classification, and journaling
    /// </summary>
    public class OperationOrchestrator
    {
        private readonly JobJournalService _journalService;
        private readonly PreflightEngine _preflightEngine;
        private readonly BackupIntegrityService _backupIntegrityService;
        private readonly ToolRegistry _toolRegistry;

        public event Action<OperationProgressUpdate>? ProgressChanged;
        public event Action<string>? LogEmitted;

        public OperationOrchestrator(
            JobJournalService journalService,
            PreflightEngine preflightEngine,
            BackupIntegrityService backupIntegrityService,
            ToolRegistry toolRegistry)
        {
            _journalService = journalService;
            _preflightEngine = preflightEngine;
            _backupIntegrityService = backupIntegrityService;
            _toolRegistry = toolRegistry;
        }

        public async Task<OperationProgressUpdate> RunOperationAsync(
            Device device,
            OperationType operationType,
            string operationTitle,
            Func<string, CancellationToken, Task<bool>> executeAction,
            Func<CancellationToken, Task<string?>>? backupAction = null,
            Func<CancellationToken, Task<bool>>? verifyAction = null,
            DeviceCapabilityProfile? capabilityProfile = null,
            string? targetPartition = null,
            string? requiredToolName = null,
            string? firmwareFilePath = null,
            CancellationToken externalCancellationToken = default)
        {
            var jobId = _journalService.GenerateJobId();
            var sw = Stopwatch.StartNew();

            var progress = new OperationProgressUpdate
            {
                JobId = jobId,
                State = OperationState.Preflight,
                Percentage = 5,
                CurrentStep = "Validasi Pra-Operasi (Preflight)...",
                CurrentPartition = targetPartition,
                Message = $"Inisialisasi {operationTitle} pada perangkat [{device.Brand} {device.Model}]",
                CanCancel = true,
                IsRetryable = true
            };

            EmitProgress(progress, sw);

            // Log header
            EmitLog($"╔════════════════════════════════════════════════════════════════╗");
            EmitLog($"║ JOB ID: {jobId,-54} ║");
            EmitLog($"║ OPERASI: {operationTitle,-53} ║");
            EmitLog($"║ PERANGKAT: {device.Brand + " " + device.Model + " (" + device.Mode + ")",-51} ║");
            EmitLog($"╚════════════════════════════════════════════════════════════════╝\n");

            // 1. Start Job Journal Entry (persisted to SQLite)
            await _journalService.StartJobAsync(
                jobId,
                device.DeviceId,
                device.Brand,
                device.Model,
                device.Chipset,
                device.PortName,
                operationType.ToString(),
                requiredToolName ?? string.Empty,
                targetPartition ?? string.Empty);

            // 2. Preflight Evaluation
            var preflight = await _preflightEngine.EvaluateAsync(
                device,
                operationType,
                capabilityProfile,
                targetPartition,
                firmwareFilePath,
                requiredToolName);

            foreach (var msg in preflight.Messages) EmitLog(msg);
            foreach (var warn in preflight.Warnings) EmitLog($"⚠️ [Peringatan] {warn}");

            if (preflight.IsBlocked)
            {
                var blockMsg = string.Join(" | ", preflight.BlockingReasons);
                EmitLog($"\n🛑 [PREFLIGHT BLOCK] Operasi dihentikan demi keamanan:\n{blockMsg}");
                if (!string.IsNullOrEmpty(preflight.RecommendedFix))
                {
                    EmitLog($"💡 [Solusi]: {preflight.RecommendedFix}");
                }

                progress.State = OperationState.Failed;
                progress.Percentage = 100;
                progress.FailureType = FailureClassification.PermissionDenied;
                progress.FailureReason = blockMsg;
                progress.RecoveryAction = preflight.RecommendedFix ?? "Perbaiki konfigurasi sebelum mencoba lagi.";
                EmitProgress(progress, sw);

                await _journalService.FailJobAsync(jobId, progress.FailureType, progress.FailureReason, progress.RecoveryAction);
                return progress;
            }

            // 3. Device Detected Phase
            progress.State = OperationState.DeviceDetected;
            progress.Percentage = 15;
            progress.CurrentStep = "Perangkat Terdeteksi & Terverifikasi";
            EmitProgress(progress, sw);
            await _journalService.UpdateJobStateAsync(jobId, OperationState.DeviceDetected);

            // Check cancellation
            if (externalCancellationToken.IsCancellationRequested)
            {
                return await HandleCancellationAsync(jobId, progress, sw);
            }

            // 4. Backup Phase (if needed or requested)
            string? createdBackupPath = null;
            string? backupSha256 = null;
            bool isBackupVerified = false;

            if (backupAction != null)
            {
                progress.State = OperationState.Backup;
                progress.Percentage = 25;
                progress.CurrentStep = $"Mencadangkan Partisi [{targetPartition ?? "Vital"}]...";
                EmitProgress(progress, sw);
                EmitLog($"\n[Backup] Memulai proses pencadangan darurat sebelum eksekusi...");
                await _journalService.UpdateJobStateAsync(jobId, OperationState.Backup);

                try
                {
                    createdBackupPath = await backupAction(externalCancellationToken);
                    if (!string.IsNullOrEmpty(createdBackupPath) && File.Exists(createdBackupPath))
                    {
                        var manifest = await _backupIntegrityService.CreateAndVerifyManifestAsync(
                            jobId,
                            device.DeviceId,
                            targetPartition ?? "auto_dump",
                            createdBackupPath,
                            device.Mode.ToString());

                        backupSha256 = manifest.Sha256Hash;
                        isBackupVerified = manifest.IsVerified;

                        if (isBackupVerified)
                        {
                            EmitLog($"🛡️ [Backup Manifest Verified] SHA-256: {backupSha256[..Math.Min(16, backupSha256.Length)]}... ({manifest.FileSizeBytes / 1024:N0} KB)");
                            await _journalService.UpdateJobStateAsync(jobId, OperationState.Backup, backupPath: createdBackupPath, backupSha256: backupSha256, backupVerified: isBackupVerified);
                        }
                        else
                        {
                            EmitLog("❌ [Backup Error] Verifikasi integritas file cadangan gagal (file kosong atau korup).");
                        }
                    }
                    else
                    {
                        EmitLog("⚠️ [Peringatan Backup] File cadangan tidak tercipta.");
                    }
                }
                catch (Exception ex)
                {
                    EmitLog($"[Backup Exception] {ex.Message}");
                }

                // Mandatory Backup Safety Lock for sensitive security partitions
                if (operationType.RequiresMandatoryBackup(targetPartition) && !isBackupVerified)
                {
                    var backupError = $"Cadangan partisi vital [{targetPartition ?? "Security"}] gagal dibuat atau tidak valid. Operasi destruktif dihentikan demi mencegah hilangnya IMEI/Sinyal.";
                    EmitLog($"\n🛑 [SAFETY LOCK BLOCKED] {backupError}");

                    progress.State = OperationState.Failed;
                    progress.Percentage = 100;
                    progress.FailureType = FailureClassification.VerificationFailed;
                    progress.FailureReason = backupError;
                    progress.RecoveryAction = "Pastikan koneksi baca memori stabil sebelum melanjutkan operasi partisi sensitif.";
                    EmitProgress(progress, sw);

                    await _journalService.FailJobAsync(jobId, progress.FailureType, progress.FailureReason, progress.RecoveryAction);
                    return progress;
                }
            }

            // Check cancellation
            if (externalCancellationToken.IsCancellationRequested)
            {
                return await HandleCancellationAsync(jobId, progress, sw);
            }

            // 5. Preparing Phase
            progress.State = OperationState.Preparing;
            progress.Percentage = 40;
            progress.CurrentStep = "Mempersiapkan Payload & Parameter Eksekusi...";
            EmitProgress(progress, sw);
            await _journalService.UpdateJobStateAsync(jobId, OperationState.Preparing);

            // 6. Executing Phase (The actual hardware protocol work)
            progress.State = OperationState.Executing;
            progress.Percentage = 50;
            progress.CurrentStep = $"Mengeksekusi {operationTitle}...";
            progress.CanCancel = false; // Critical section: cancellation during write could brick flash memory
            EmitProgress(progress, sw);
            await _journalService.UpdateJobStateAsync(jobId, OperationState.Executing);

            bool executionSuccess = false;
            try
            {
                executionSuccess = await executeAction(jobId, externalCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return await HandleCancellationAsync(jobId, progress, sw);
            }
            catch (Exception ex)
            {
                EmitLog($"[Execution Exception] {ex.Message}");
                progress.FailureType = UsbRecoveryService.ClassifyFailure(ex.Message, -1, false, false);
                progress.FailureReason = ex.Message;
            }

            if (!executionSuccess && progress.FailureType == FailureClassification.None)
            {
                progress.FailureType = FailureClassification.UnknownFailure;
                progress.FailureReason = "Eksekusi protokol hardware mengembalikan status gagal atau response error.";
            }

            // 7. Verifying Phase
            if (executionSuccess)
            {
                progress.State = OperationState.Verifying;
                progress.Percentage = 85;
                progress.CurrentStep = "Memverifikasi Integritas Pasca-Eksekusi...";
                EmitProgress(progress, sw);
                await _journalService.UpdateJobStateAsync(jobId, OperationState.Verifying);

                if (verifyAction != null)
                {
                    var isVerified = await verifyAction(externalCancellationToken);
                    if (!isVerified)
                    {
                        executionSuccess = false;
                        progress.FailureType = FailureClassification.VerificationFailed;
                        progress.FailureReason = "Verifikasi integritas data pasca-eksekusi gagal.";
                    }
                }
            }

            // 8. Completed or Failed / Recovery Required Phase
            if (executionSuccess)
            {
                progress.State = OperationState.Completed;
                progress.Percentage = 100;
                progress.CurrentStep = "Operasi Berhasil!";
                progress.Message = $"Operasi {operationTitle} sukses diselesaikan.";
                progress.IsRetryable = true;
                EmitProgress(progress, sw);

                EmitLog($"\n════════════════════════════════════════════════════════════════");
                EmitLog($" [SUKSES] {operationTitle} Selesai dengan Sempurna! (Durasi: {sw.Elapsed.TotalSeconds:N1}s)");
                EmitLog($"════════════════════════════════════════════════════════════════\n");

                await _journalService.CompleteJobAsync(jobId, true);
            }
            else
            {
                // Blind Retry Prevention & Recovery Advisor
                var isSafeToRetry = UsbRecoveryService.CanSafelyRetry(operationType, progress.State, progress.FailureType);
                progress.IsRetryable = isSafeToRetry;

                var (summary, actionSteps) = UsbRecoveryService.GetSafeRecoveryGuide(
                    device.Brand, 
                    device.Chipset, 
                    operationType, 
                    progress.State, 
                    targetPartition);

                progress.RecoverySteps = actionSteps;
                progress.RecoveryAction = summary;

                if (!isSafeToRetry && operationType.IsDestructive())
                {
                    progress.State = OperationState.RecoveryRequired;
                    EmitLog($"\n⚠️ [BLIND RETRY LOCK] Operasi penulisan chip flash terganggu. Pengulangan otomatis diblokir demi mencegah kerusakan memori fisik.");
                }
                else
                {
                    progress.State = OperationState.Failed;
                }

                progress.Percentage = 100;
                progress.CurrentStep = progress.State == OperationState.RecoveryRequired ? "Pemulihan Perangkat Diperlukan" : "Operasi Gagal";
                progress.Message = $"Gagal mengeksekusi {operationTitle}: {progress.FailureReason}";
                EmitProgress(progress, sw);

                EmitLog($"\n❌ [{progress.State}] {operationTitle} Gagal: {progress.FailureReason}");
                EmitLog($"💡 [Panduan Pemulihan]: {summary}");
                foreach (var step in actionSteps)
                {
                    EmitLog($"   • {step}");
                }

                await _journalService.FailJobAsync(jobId, progress.FailureType, progress.FailureReason ?? "Unknown Error", progress.RecoveryAction ?? "Periksa kabel dan ulangi.");
            }

            return progress;
        }

        private async Task<OperationProgressUpdate> HandleCancellationAsync(string jobId, OperationProgressUpdate progress, Stopwatch sw)
        {
            progress.State = OperationState.Cancelled;
            progress.Percentage = 100;
            progress.CurrentStep = "Dibatalkan oleh Operator";
            progress.FailureType = FailureClassification.Cancelled;
            progress.FailureReason = "Operasi dibatalkan secara manual oleh pengguna sebelum fase kritis.";
            progress.RecoveryAction = "Perangkat dalam keadaan aman. Anda dapat mengulangi operasi kapan saja.";
            progress.IsRetryable = true;
            EmitProgress(progress, sw);

            EmitLog("\n⚠️ [DIBATALKAN] Operasi dihentikan dengan aman sebelum penulisan chip memori.");
            await _journalService.FailJobAsync(jobId, FailureClassification.Cancelled, progress.FailureReason, progress.RecoveryAction);
            return progress;
        }

        private void EmitProgress(OperationProgressUpdate progress, Stopwatch sw)
        {
            progress.ElapsedTime = sw.Elapsed;
            ProgressChanged?.Invoke(progress);
        }

        private void EmitLog(string message)
        {
            LogEmitted?.Invoke(message);
        }
    }
}
