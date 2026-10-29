using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Core engine for executing device operations via external tools, centrally managed through ProcessExecutionService
    /// </summary>
    public class ToolExecutionEngine
    {
        private readonly string _toolsBasePath;
        private readonly string _platformToolsPath;
        private readonly ToolRegistry _toolRegistry;
        private readonly ProcessExecutionService _processExecutor;

        public ToolExecutionEngine(string toolsBasePath, ToolRegistry? toolRegistry = null, ProcessExecutionService? processExecutor = null)
        {
            _toolsBasePath = toolsBasePath;
            _platformToolsPath = Path.Combine(toolsBasePath, "platform-tools");
            _toolRegistry = toolRegistry ?? new ToolRegistry(toolsBasePath);
            _processExecutor = processExecutor ?? new ProcessExecutionService();
        }

        public ToolRegistry Registry => _toolRegistry;
        public ProcessExecutionService ProcessExecutor => _processExecutor;

        public event Action<int>? OnPercentageProgress;
        public event Action<ErrorDiagnosis>? OnErrorDiagnosed;

        private Action<string>? WrapOutputHandler(Action<string>? originalHandler)
        {
            return (line) =>
            {
                if (ProgressStreamParser.TryParseProgress(line, out int pct))
                {
                    OnPercentageProgress?.Invoke(pct);
                }

                var diag = SmartErrorTranslator.TranslateError(line);
                if (diag != null)
                {
                    OnErrorDiagnosed?.Invoke(diag);
                    originalHandler?.Invoke($"[Diagnosis] ⚠️ {diag.Meaning}\n[Solusi] 💡 {diag.ActionableSolution}");
                }

                originalHandler?.Invoke(line);
            };
        }

        /// <summary>
        /// Execute Samsung Odin flashing (supports 4-file BL, AP, CP, CSC + PIT)
        /// </summary>
        public async Task<OperationResult> ExecuteSamsungFlashAsync(
            string deviceId, 
            string firmwarePath, 
            OperationParameters parameters, 
            int timeoutMs = 300000, 
            CancellationToken cancellationToken = default, 
            Action<string>? onProgressOutput = null)
        {
            var result = new OperationResult { Success = false };
            var odinPath = Path.Combine(_toolsBasePath, "samsung", "odin.exe");
            if (!File.Exists(odinPath))
            {
                odinPath = _toolRegistry.GetToolPath("odin") ?? odinPath;
            }

            if (!File.Exists(odinPath))
            {
                result.ErrorMessage = "Odin tool tidak ditemukan. Pastikan odin.exe terpasang di tools/samsung/";
                return result;
            }

            bool hasValidFile = File.Exists(firmwarePath) ||
                                (!string.IsNullOrEmpty(parameters.BlPath) && File.Exists(parameters.BlPath)) ||
                                (!string.IsNullOrEmpty(parameters.ApPath) && File.Exists(parameters.ApPath)) ||
                                (!string.IsNullOrEmpty(parameters.CpPath) && File.Exists(parameters.CpPath)) ||
                                (!string.IsNullOrEmpty(parameters.CscPath) && File.Exists(parameters.CscPath));

            if (!hasValidFile)
            {
                result.ErrorMessage = $"Berkas firmware Samsung tidak ditemukan atau path tidak valid.";
                return result;
            }

            var args = BuildOdinArguments(deviceId, firmwarePath, parameters);
            var procResult = await _processExecutor.ExecuteAsync(odinPath, args, Path.GetDirectoryName(odinPath), timeoutMs, cancellationToken, onProgressOutput);

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Execute Xiaomi EDL flashing via MiFlash
        /// </summary>
        public async Task<OperationResult> ExecuteXiaomiFlashAsync(
            string deviceId, 
            string firmwarePath, 
            OperationParameters parameters, 
            int timeoutMs = 300000, 
            CancellationToken cancellationToken = default, 
            Action<string>? onProgressOutput = null)
        {
            var result = new OperationResult { Success = false };
            var miFlashPath = Path.Combine(_toolsBasePath, "xiaomi", "XiaoMiFlash.exe");
            if (!File.Exists(miFlashPath))
            {
                miFlashPath = _toolRegistry.GetToolPath("miflash") ?? miFlashPath;
            }

            if (!File.Exists(miFlashPath))
            {
                result.ErrorMessage = "MiFlash tool not found. Please place XiaoMiFlash.exe in tools/xiaomi/";
                return result;
            }

            if (!Directory.Exists(firmwarePath))
            {
                result.ErrorMessage = $"Firmware directory not found: {firmwarePath}";
                return result;
            }

            var args = BuildXiaomiArguments(deviceId, firmwarePath, parameters);
            var procResult = await _processExecutor.ExecuteAsync(miFlashPath, args, Path.GetDirectoryName(miFlashPath), timeoutMs, cancellationToken, onProgressOutput);

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        public async Task<OperationResult> ErasePartitionFastbootAsync(string deviceId, string partition, CancellationToken cancellationToken = default)
        {
            return await ExecuteFastbootCommandAsync(deviceId, $"erase {partition}", 30000, cancellationToken);
        }

        public async Task<OperationResult> FormatExt4FastbootAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return await ExecuteFastbootCommandAsync(deviceId, "format:ext4", 120000, cancellationToken);
        }

        /// <summary>
        /// Execute Oppo download mode flashing
        /// </summary>
        public async Task<OperationResult> ExecuteOppoFlashAsync(
            string deviceId, 
            string romPath, 
            OperationParameters parameters, 
            int timeoutMs = 600000, 
            CancellationToken cancellationToken = default, 
            Action<string>? onProgressOutput = null)
        {
            var result = new OperationResult { Success = false };
            var oppoToolPath = Path.Combine(_toolsBasePath, "oppo", "DownloadTool.exe");

            if (!File.Exists(oppoToolPath))
            {
                result.ErrorMessage = "Oppo DownloadTool not found. Please place DownloadTool.exe in tools/oppo/";
                return result;
            }

            var args = BuildOppoArguments(deviceId, romPath, parameters);
            var procResult = await _processExecutor.ExecuteAsync(oppoToolPath, args, Path.GetDirectoryName(oppoToolPath), timeoutMs, cancellationToken, onProgressOutput);

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Execute ADB command
        /// </summary>
        public async Task<OperationResult> ExecuteAdbCommandAsync(
            string deviceId, 
            string command, 
            int timeoutMs = 60000, 
            CancellationToken cancellationToken = default, 
            Action<string>? onProgressOutput = null)
        {
            var result = new OperationResult { Success = false };
            var adbPath = _toolRegistry.GetToolPath("adb") ?? Path.Combine(_platformToolsPath, "adb.exe");

            if (!File.Exists(adbPath))
            {
                result.ErrorMessage = "ADB not found. Please place adb.exe in tools/platform-tools/";
                return result;
            }

            var args = string.IsNullOrEmpty(deviceId) ? command : $"-s {deviceId} {command}";
            var procResult = await _processExecutor.ExecuteAsync(adbPath, args, Path.GetDirectoryName(adbPath), timeoutMs, cancellationToken, onProgressOutput);

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Execute Fastboot command
        /// </summary>
        public async Task<OperationResult> ExecuteFastbootCommandAsync(
            string deviceId, 
            string command, 
            int timeoutMs = 60000, 
            CancellationToken cancellationToken = default, 
            Action<string>? onProgressOutput = null)
        {
            var result = new OperationResult { Success = false };
            var fastbootPath = _toolRegistry.GetToolPath("fastboot") ?? Path.Combine(_platformToolsPath, "fastboot.exe");

            if (!File.Exists(fastbootPath))
            {
                result.ErrorMessage = "Fastboot not found. Please place fastboot.exe in tools/platform-tools/";
                return result;
            }

            var args = string.IsNullOrEmpty(deviceId) ? command : $"-s {deviceId} {command}";
            var procResult = await _processExecutor.ExecuteAsync(fastbootPath, args, Path.GetDirectoryName(fastbootPath), timeoutMs, cancellationToken, onProgressOutput);

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Install APK via ADB
        /// </summary>
        public async Task<OperationResult> InstallApkAsync(string deviceId, string apkPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(apkPath))
            {
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = $"APK file not found: {apkPath}"
                };
            }

            return await ExecuteAdbCommandAsync(deviceId, $"install \"{apkPath}\"", 120000, cancellationToken);
        }

        /// <summary>
        /// Boot to recovery via Fastboot
        /// </summary>
        public async Task<OperationResult> BootRecoveryAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return await ExecuteFastbootCommandAsync(deviceId, "boot recovery.img", 60000, cancellationToken);
        }

        /// <summary>
        /// Flash recovery via Fastboot
        /// </summary>
        public async Task<OperationResult> FlashRecoveryAsync(string deviceId, string recoveryPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(recoveryPath))
            {
                return new OperationResult
                {
                    Success = false,
                    ErrorMessage = $"Recovery image not found: {recoveryPath}"
                };
            }

            return await ExecuteFastbootCommandAsync(deviceId, $"flash recovery \"{recoveryPath}\"", 120000, cancellationToken);
        }

        /// <summary>
        /// Unlock bootloader via Fastboot
        /// </summary>
        public async Task<OperationResult> UnlockBootloaderAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return await ExecuteFastbootCommandAsync(deviceId, "flashing unlock", 60000, cancellationToken);
        }

        /// <summary>
        /// Format device via Fastboot
        /// </summary>
        public async Task<OperationResult> FormatDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return await ExecuteFastbootCommandAsync(deviceId, "format:ext4", 120000, cancellationToken);
        }

        // Helper methods for building tool-specific arguments
        private string BuildOdinArguments(string deviceId, string firmwarePath, OperationParameters parameters)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"-device {deviceId}");

            if (!string.IsNullOrEmpty(parameters.BlPath) && File.Exists(parameters.BlPath))
                sb.Append($" -bl \"{parameters.BlPath}\"");
            if (!string.IsNullOrEmpty(parameters.ApPath) && File.Exists(parameters.ApPath))
                sb.Append($" -ap \"{parameters.ApPath}\"");
            if (!string.IsNullOrEmpty(parameters.CpPath) && File.Exists(parameters.CpPath))
                sb.Append($" -cp \"{parameters.CpPath}\"");
            if (!string.IsNullOrEmpty(parameters.CscPath) && File.Exists(parameters.CscPath))
                sb.Append($" -csc \"{parameters.CscPath}\"");
            if (!string.IsNullOrEmpty(parameters.PitPath) && File.Exists(parameters.PitPath))
                sb.Append($" -pit \"{parameters.PitPath}\"");

            // Single file fallback
            if (string.IsNullOrEmpty(parameters.BlPath) && string.IsNullOrEmpty(parameters.ApPath) &&
                string.IsNullOrEmpty(parameters.CpPath) && string.IsNullOrEmpty(parameters.CscPath) &&
                !string.IsNullOrEmpty(firmwarePath))
            {
                sb.Append($" -file \"{firmwarePath}\"");
            }

            if (parameters.KeepData) sb.Append(" -keepdata");
            if (parameters.AutoReboot) sb.Append(" -autoreboot");
            return sb.ToString();
        }

        private string BuildXiaomiArguments(string deviceId, string firmwarePath, OperationParameters parameters)
        {
            var script = parameters.KeepData ? "flash_all_except_data_storage.bat" : "flash_all.bat";
            return $"-s {deviceId} -p \"{firmwarePath}\" -f {script}";
        }

        /// <summary>
        /// Execute mtkclient for MediaTek BROM operations (SLA Bypass, FRP, etc.)
        /// </summary>
        public async Task<OperationResult> ExecuteMtkClientAsync(
            string arguments, 
            Action<string>? onProgressOutput = null, 
            int timeoutMs = 120000, 
            CancellationToken cancellationToken = default)
        {
            var result = new OperationResult { Success = false };
            
            // Resolve Python and MTK script paths
            var pythonExePath = _toolRegistry.GetToolPath("python") 
                ?? Path.Combine(_toolsBasePath, "runtime", "python", "python.exe");
            if (!File.Exists(pythonExePath))
            {
                pythonExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "runtime", "python", "python.exe");
            }

            var mtkPyPath = _toolRegistry.GetToolPath("mtkclient")
                ?? Path.Combine(_toolsBasePath, "mtkclient", "mtk.py");
            if (!File.Exists(mtkPyPath))
            {
                mtkPyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "mtkclient", "mtk.py");
            }

            var mtkExePath = Path.Combine(_toolsBasePath, "mtkclient", "mtk.exe");

            string fileName;
            string finalArgs;
            string workingDirectory;

            if (File.Exists(mtkExePath) && new FileInfo(mtkExePath).Length > 100000)
            {
                fileName = mtkExePath;
                finalArgs = arguments;
                workingDirectory = Path.GetDirectoryName(mtkExePath) ?? _toolsBasePath;
            }
            else if (File.Exists(pythonExePath) && File.Exists(mtkPyPath))
            {
                fileName = pythonExePath;
                finalArgs = $"\"{mtkPyPath}\" {arguments}";
                workingDirectory = Path.GetDirectoryName(mtkPyPath) ?? _toolsBasePath;
            }
            else
            {
                result.ErrorMessage = "Alat mtkclient tidak ditemukan. Pastikan mtkclient terpasang di tools/mtkclient/ dan runtime python tersedia.";
                return result;
            }

            var procResult = await _processExecutor.ExecuteAsync(fileName, finalArgs, workingDirectory, timeoutMs, cancellationToken, WrapOutputHandler(onProgressOutput));

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Execute Qualcomm EDL operations using bkerler/edl or similar open-source tool
        /// </summary>
        public async Task<OperationResult> ExecuteQualcommEdlAsync(
            string? loaderPath, 
            string arguments, 
            Action<string>? onProgressOutput = null, 
            int timeoutMs = 120000, 
            CancellationToken cancellationToken = default)
        {
            var result = new OperationResult { Success = false };

            // Resolve Python and EDL script paths
            var pythonExePath = _toolRegistry.GetToolPath("python") 
                ?? Path.Combine(_toolsBasePath, "runtime", "python", "python.exe");
            if (!File.Exists(pythonExePath))
            {
                pythonExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "runtime", "python", "python.exe");
            }

            var edlPyPath = _toolRegistry.GetToolPath("qcom-edl")
                ?? Path.Combine(_toolsBasePath, "qcom-edl", "edl.py");
            if (!File.Exists(edlPyPath))
            {
                edlPyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "qcom-edl", "edl.py");
            }

            var edlExePath = Path.Combine(_toolsBasePath, "qcom-edl", "edl.exe");

            string fileName;
            string finalArgs;
            string workingDirectory;

            var loaderArg = !string.IsNullOrWhiteSpace(loaderPath) ? $"--loader \"{loaderPath}\" " : "";

            if (File.Exists(edlExePath) && new FileInfo(edlExePath).Length > 100000)
            {
                fileName = edlExePath;
                finalArgs = $"{loaderArg}{arguments}".Trim();
                workingDirectory = Path.GetDirectoryName(edlExePath) ?? _toolsBasePath;
            }
            else if (File.Exists(pythonExePath) && File.Exists(edlPyPath))
            {
                fileName = pythonExePath;
                finalArgs = $"\"{edlPyPath}\" {loaderArg}{arguments}".Trim();
                workingDirectory = Path.GetDirectoryName(edlPyPath) ?? _toolsBasePath;
            }
            else
            {
                result.ErrorMessage = "Alat edl tidak ditemukan di tools/qcom-edl/ atau runtime python.";
                return result;
            }

            var procResult = await _processExecutor.ExecuteAsync(fileName, finalArgs, workingDirectory, timeoutMs, cancellationToken, WrapOutputHandler(onProgressOutput));

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }

        private string BuildMiFlashArguments(string deviceId, string romPath, OperationParameters parameters)
        {
            var args = $"--device {deviceId} --rom \"{romPath}\"";
            if (parameters.KeepData) args += " --keepdata";
            if (parameters.AutoReboot) args += " --autoreboot";
            return args;
        }

        private string BuildOppoArguments(string deviceId, string romPath, OperationParameters parameters)
        {
            var args = $"-d {deviceId} -f \"{romPath}\"";
            if (parameters.KeepData) args += " -k";
            if (parameters.AutoReboot) args += " -r";
            return args;
        }

        /// <summary>
        /// Execute Unisoc / Spreadtrum ResearchDownload PAC Flashing
        /// </summary>
        public async Task<OperationResult> ExecuteSpdResearchDownloadAsync(
            string pacFilePath, 
            Action<string>? onProgressOutput = null, 
            int timeoutMs = 600000, 
            CancellationToken cancellationToken = default)
        {
            var result = new OperationResult { Success = false };
            var spdExePath = Path.Combine(_toolsBasePath, "spd", "ResearchDownload.exe");
            if (!File.Exists(spdExePath))
            {
                spdExePath = Path.Combine(_toolsBasePath, "spd", "UpgradeDownload.exe");
            }

            if (!File.Exists(pacFilePath))
            {
                result.ErrorMessage = $"File PAC tidak ditemukan: {pacFilePath}";
                return result;
            }

            if (!File.Exists(spdExePath))
            {
                result.Success = false;
                result.ErrorMessage = "Alat ResearchDownload.exe / UpgradeDownload.exe tidak ditemukan di folder tools/spd/. Silakan pasang paket flasher Unisoc.";
                onProgressOutput?.Invoke($"[ERROR] {result.ErrorMessage}");
                return result;
            }

            var procResult = await _processExecutor.ExecuteAsync(spdExePath, $"-pac \"{pacFilePath}\"", Path.GetDirectoryName(spdExePath), timeoutMs, cancellationToken, onProgressOutput);

            result.Success = procResult.Success;
            result.Output = procResult.CombinedOutput;
            result.ErrorMessage = procResult.Success ? null : (procResult.ExecutionError ?? procResult.StandardError);
            result.ExitCode = procResult.ExitCode;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Result of tool execution
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; set; }
        public string? Output { get; set; }
        public string? ErrorMessage { get; set; }
        public int ExitCode { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; }
        public long DurationMs => (long)(EndTime - StartTime).TotalMilliseconds;
    }
}
