using System;
using System.Windows;

namespace JumperTools.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Initialize SQLite provider batteries before any SQLite connection
                try { SQLitePCL.Batteries_V2.Init(); } catch { }

                base.OnStartup(e);

                // Initialize logging
                JumperTools.Utilities.Logger.Information("JumperTools Pro starting...");

                // Ensure all native runtimes, DLLs, and PATH variables are self-healed on startup
                EnsureRuntimeIntegrity();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "JumperTools Pro Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void EnsureRuntimeIntegrity()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var pythonDir = System.IO.Path.Combine(baseDir, "tools", "runtime", "python");
                var mtkWinDir = System.IO.Path.Combine(baseDir, "tools", "mtkclient", "mtkclient", "Windows");
                var platformToolsDir = System.IO.Path.Combine(baseDir, "tools", "platform-tools");
                var edlWinDir = System.IO.Path.Combine(baseDir, "tools", "qcom-edl", "edlclient", "Windows");

                // 1. Auto-deploy libusb-1.0.dll ke python runtime jika belum ada
                if (System.IO.Directory.Exists(pythonDir))
                {
                    var targetLibusb = System.IO.Path.Combine(pythonDir, "libusb-1.0.dll");
                    if (!System.IO.File.Exists(targetLibusb))
                    {
                        var sourceLibusb = System.IO.Path.Combine(mtkWinDir, "libusb-1.0.dll");
                        if (System.IO.File.Exists(sourceLibusb))
                        {
                            System.IO.File.Copy(sourceLibusb, targetLibusb, true);
                        }
                    }
                }

                // 2. Set PATH process environment agar Windows dapat memuat semua DLL native
                var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
                var pathsToAdd = new[] { pythonDir, mtkWinDir, edlWinDir, platformToolsDir };
                foreach (var p in pathsToAdd)
                {
                    if (System.IO.Directory.Exists(p) && !currentPath.Contains(p, StringComparison.OrdinalIgnoreCase))
                    {
                        currentPath = p + ";" + currentPath;
                    }
                }
                Environment.SetEnvironmentVariable("PATH", currentPath, EnvironmentVariableTarget.Process);
            }
            catch (Exception ex)
            {
                JumperTools.Utilities.Logger.Warning($"Runtime integrity check warning: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                JumperTools.Utilities.Logger.Information("JumperTools Pro shutting down...");
                var processes = new[] { "python", "mtk", "fastboot", "edl" };
                foreach (var p in processes)
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName(p))
                    {
                        try { proc.Kill(true); } catch { }
                    }
                }
                JumperTools.Utilities.Logger.CloseAndFlush();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Exit Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            base.OnExit(e);
        }
    }
}
