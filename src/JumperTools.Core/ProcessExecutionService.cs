using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Core
{
    public class ProcessExecutionResult
    {
        public bool Success => ExitCode == 0 && !TimedOut && !WasCancelled && string.IsNullOrEmpty(ExecutionError);
        public int ExitCode { get; set; } = -1;
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public string CombinedOutput { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public bool TimedOut { get; set; }
        public bool WasCancelled { get; set; }
        public string? ExecutionError { get; set; }
    }

    /// <summary>
    /// Unified asynchronous process execution service with streaming output, cancellation, timeout, and process tree termination
    /// </summary>
    public class ProcessExecutionService
    {
        public event Action<string>? OutputLineReceived;
        public event Action<string>? ErrorLineReceived;

        public async Task<ProcessExecutionResult> ExecuteAsync(
            string executablePath,
            string arguments,
            string? workingDirectory = null,
            int timeoutMs = 60000,
            CancellationToken cancellationToken = default,
            Action<string>? onOutputLine = null)
        {
            var result = new ProcessExecutionResult();
            var sw = Stopwatch.StartNew();

            if (!File.Exists(executablePath))
            {
                result.ExecutionError = $"Executable tidak ditemukan: {executablePath}";
                result.ExitCode = -1;
                return result;
            }

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();
            var combinedBuilder = new StringBuilder();

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (stdoutBuilder)
                    {
                        stdoutBuilder.AppendLine(e.Data);
                        combinedBuilder.AppendLine(e.Data);
                    }
                    OutputLineReceived?.Invoke(e.Data);
                    onOutputLine?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    lock (stderrBuilder)
                    {
                        stderrBuilder.AppendLine(e.Data);
                        combinedBuilder.AppendLine(e.Data);
                    }
                    ErrorLineReceived?.Invoke(e.Data);
                    onOutputLine?.Invoke($"[STDERR] {e.Data}");
                }
            };

            try
            {
                if (!process.Start())
                {
                    result.ExecutionError = "Gagal memulai proses eksekusi CLI.";
                    result.ExitCode = -1;
                    return result;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                    result.ExitCode = process.ExitCode;
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        result.TimedOut = true;
                        result.ExecutionError = $"Operasi dibatalkan karena timeout ({timeoutMs / 1000}s).";
                    }
                    else
                    {
                        result.WasCancelled = true;
                        result.ExecutionError = "Operasi dibatalkan oleh operator.";
                    }

                    KillProcessTree(process);
                }
            }
            catch (Exception ex)
            {
                result.ExecutionError = ex.Message;
                result.ExitCode = -1;
                KillProcessTree(process);
            }
            finally
            {
                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;
                result.StandardOutput = stdoutBuilder.ToString().Trim();
                result.StandardError = stderrBuilder.ToString().Trim();
                result.CombinedOutput = combinedBuilder.ToString().Trim();
            }

            return result;
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (process.HasExited) return;

                // Attempt graceful taskkill on Windows to kill all child subprocesses
                try
                {
                    using var killProc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {process.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    killProc?.WaitForExit(2000);
                }
                catch
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { }
        }
    }
}
