using Serilog;
using System;
using System.IO;

namespace JumperTools.Utilities
{
    /// <summary>
    /// Centralized logging utility using Serilog
    /// </summary>
    public static class Logger
    {
        static Logger()
        {
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var logPath = Path.Combine(logDir, "hptools-.txt");

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                // Fallback to console only if file logging fails
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .CreateLogger();
                
                Log.Error(ex, "Failed to initialize file logging, using console only");
            }
        }

        public static void Information(string message, params object[] args)
        {
            Log.Information(message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            Log.Warning(message, args);
        }

        public static void Error(string message, params object[] args)
        {
            Log.Error(message, args);
        }

        public static void Error(Exception ex, string message, params object[] args)
        {
            Log.Error(ex, message, args);
        }

        public static void Debug(string message, params object[] args)
        {
            Log.Debug(message, args);
        }

        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
