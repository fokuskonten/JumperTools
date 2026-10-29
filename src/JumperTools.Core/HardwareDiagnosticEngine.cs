using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Engine for running non-destructive hardware diagnostic tests.
    /// This is strictly for Product Validation and gap closure.
    /// </summary>
    public class HardwareDiagnosticEngine
    {
        private readonly ToolExecutionEngine _toolEngine;
        private readonly string _platformToolsPath;

        public event Action<string>? OnLog;

        public HardwareDiagnosticEngine(ToolExecutionEngine toolEngine, string platformToolsPath)
        {
            _toolEngine = toolEngine;
            _platformToolsPath = platformToolsPath;
        }

        private void Log(string message) => OnLog?.Invoke($"[DIAGNOSTIC] {message}");

        public async Task<HardwareTestResult> TestFastbootDetectionAsync(CancellationToken cancellationToken = default)
        {
            var result = new HardwareTestResult
            {
                Platform = "Android",
                Mode = "Fastboot",
                Tool = "fastboot.exe",
                Operation = "Device Detection Test",
                Timestamp = DateTime.Now
            };

            try
            {
                Log("Starting Fastboot Detection Test...");
                var response = await _toolEngine.ProcessExecutor.ExecuteAsync(
                    System.IO.Path.Combine(_platformToolsPath, "fastboot.exe"),
                    "devices",
                    _platformToolsPath,
                    60000,
                    cancellationToken);

                result.Log = response.CombinedOutput;

                if (response.ExitCode == 0 && response.CombinedOutput.Contains("fastboot"))
                {
                    result.Result = "PASS";
                    result.HardwareType = "REAL HARDWARE";
                    Log("Fastboot test passed. Device detected.");
                }
                else
                {
                    result.Result = "NOT TESTED"; // Assumed no device connected
                    result.HardwareType = "NOT TESTED";
                    result.Error = "No fastboot device found. Please connect hardware to validate.";
                    Log("Fastboot test aborted: No physical device connected.");
                }
            }
            catch (Exception ex)
            {
                result.Result = "FAIL";
                result.Error = ex.Message;
                Log($"Fastboot test failed: {ex.Message}");
            }

            return result;
        }

        public async Task<HardwareTestResult> TestAdbDetectionAsync(CancellationToken cancellationToken = default)
        {
            var result = new HardwareTestResult
            {
                Platform = "Android",
                Mode = "ADB/MTP",
                Tool = "adb.exe",
                Operation = "Handshake Test",
                Timestamp = DateTime.Now
            };

            try
            {
                Log("Starting ADB Detection Test...");
                var response = await _toolEngine.ProcessExecutor.ExecuteAsync(
                    System.IO.Path.Combine(_platformToolsPath, "adb.exe"),
                    "devices",
                    _platformToolsPath,
                    60000,
                    cancellationToken);

                result.Log = response.CombinedOutput;

                if (response.ExitCode == 0 && response.CombinedOutput.Contains("\tdevice"))
                {
                    result.Result = "PASS";
                    result.HardwareType = "REAL HARDWARE";
                    Log("ADB test passed. Device detected.");
                }
                else
                {
                    result.Result = "NOT TESTED";
                    result.HardwareType = "NOT TESTED";
                    result.Error = "No ADB device found. Please connect hardware to validate.";
                    Log("ADB test aborted: No physical device connected.");
                }
            }
            catch (Exception ex)
            {
                result.Result = "FAIL";
                result.Error = ex.Message;
                Log($"ADB test failed: {ex.Message}");
            }

            return result;
        }

        public async Task<HardwareTestResult> TestMtkBromDetectionAsync(CancellationToken cancellationToken = default)
        {
            var result = new HardwareTestResult
            {
                Platform = "MediaTek",
                Mode = "BROM",
                Tool = "mtkclient",
                Operation = "Protocol Initialization Test",
                Timestamp = DateTime.Now
            };

            try
            {
                Log("Starting MTK BROM Detection Test...");
                var response = await _toolEngine.ExecuteMtkClientAsync(
                    "printgpt --skip", 
                    Log,
                    60000,
                    cancellationToken);

                result.Log = response.Output ?? string.Empty;

                if (response.Success || (response.Output != null && (response.Output.Contains("Preloader") || response.Output.Contains("DA"))))
                {
                    result.Result = "PASS";
                    result.HardwareType = "REAL HARDWARE";
                    Log("MTK BROM test passed.");
                }
                else
                {
                    result.Result = "NOT TESTED";
                    result.HardwareType = "NOT TESTED";
                    result.Error = "Hardware not connected or MTK BROM handshake not initiated.";
                    Log("MTK test aborted: No hardware.");
                }
            }
            catch (Exception ex)
            {
                result.Result = "FAIL";
                result.Error = ex.Message;
                Log($"MTK BROM test failed: {ex.Message}");
            }

            return result;
        }

        public async Task<HardwareTestResult> TestQualcommEdlDetectionAsync(CancellationToken cancellationToken = default)
        {
            var result = new HardwareTestResult
            {
                Platform = "Qualcomm",
                Mode = "EDL 9008",
                Tool = "qcom-edl",
                Operation = "Read/Identify Test",
                Timestamp = DateTime.Now
            };

            try
            {
                Log("Starting Qualcomm EDL Detection Test...");
                var response = await _toolEngine.ExecuteQualcommEdlAsync(
                    null,
                    "printgpt",
                    Log,
                    60000,
                    cancellationToken);

                result.Log = response.Output ?? string.Empty;

                if (response.Success || (response.Output != null && (response.Output.Contains("Sector") || response.Output.Contains("Qualcomm"))))
                {
                    result.Result = "PASS";
                    result.HardwareType = "REAL HARDWARE";
                    Log("Qualcomm EDL test passed.");
                }
                else
                {
                    result.Result = "NOT TESTED";
                    result.HardwareType = "NOT TESTED";
                    result.Error = "No EDL 9008 device detected.";
                    Log("EDL test aborted: No hardware.");
                }
            }
            catch (Exception ex)
            {
                result.Result = "FAIL";
                result.Error = ex.Message;
                Log($"Qualcomm EDL test failed: {ex.Message}");
            }

            return result;
        }
        
        public async Task<List<HardwareTestResult>> RunAllSafeTestsAsync(CancellationToken cancellationToken = default)
        {
            Log("=== INITIATING FULL HARDWARE DIAGNOSTIC SUITE ===");
            var results = new List<HardwareTestResult>();
            
            results.Add(await TestFastbootDetectionAsync(cancellationToken));
            results.Add(await TestAdbDetectionAsync(cancellationToken));
            results.Add(await TestMtkBromDetectionAsync(cancellationToken));
            results.Add(await TestQualcommEdlDetectionAsync(cancellationToken));

            Log("=== FULL HARDWARE DIAGNOSTIC SUITE COMPLETED ===");
            return results;
        }
    }
}
