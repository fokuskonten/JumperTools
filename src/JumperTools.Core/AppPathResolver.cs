using System;
using System.IO;

namespace JumperTools.Core
{
    /// <summary>
    /// Single Source of Truth (SSOT) Path Resolver for JumperTools.
    /// Dynamically resolves the true application root across Development, Debug, Test, and Production Standalone environments.
    /// Eliminates all brittle relative ladder paths (../../..).
    /// </summary>
    public static class AppPathResolver
    {
        private static readonly Lazy<string> _resolvedRoot = new(FindApplicationRoot);

        public static string RootDirectory => _resolvedRoot.Value;

        public static string ToolsDirectory => Path.Combine(RootDirectory, "tools");

        public static string DatabaseDirectory => Path.Combine(RootDirectory, "database");

        public static string PlatformToolsDirectory => Path.Combine(ToolsDirectory, "platform-tools");

        public static string AdbExecutablePath => Path.Combine(PlatformToolsDirectory, "adb.exe");

        public static string FastbootExecutablePath => Path.Combine(PlatformToolsDirectory, "fastboot.exe");

        public static string PythonExecutablePath => Path.Combine(ToolsDirectory, "runtime", "python", "python.exe");

        public static string DriversDirectory => Path.Combine(ToolsDirectory, "drivers");

        public static string MtkClientDirectory => Path.Combine(ToolsDirectory, "mtkclient");

        public static string QcomEdlDirectory => Path.Combine(ToolsDirectory, "qcom-edl");

        public static string TestPointsMappingJson => Path.Combine(DatabaseDirectory, "testpoints_mapping.json");

        public static string IspPinoutsMappingJson => Path.Combine(DatabaseDirectory, "isp_pinouts_mapping.json");

        public static string FirehoseDirectory => Path.Combine(DatabaseDirectory, "firehose");

        public static string DaDirectory => Path.Combine(DatabaseDirectory, "da");

        public static string FdlDirectory => Path.Combine(DatabaseDirectory, "fdl");

        public static string KirinDirectory => Path.Combine(DatabaseDirectory, "kirin");

        public static string PitDirectory => Path.Combine(DatabaseDirectory, "pit");

        public static string BackupsDirectory => Path.Combine(RootDirectory, "backups");

        public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

        public static string ReceiptsDirectory => Path.Combine(RootDirectory, "receipts");

        public static string ExtractedDirectory => Path.Combine(RootDirectory, "extracted");

        private static string FindApplicationRoot()
        {
            var candidateDirs = new List<string>();

            try
            {
                var procPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(procPath))
                {
                    var pDir = Path.GetDirectoryName(procPath);
                    if (!string.IsNullOrEmpty(pDir)) candidateDirs.Add(pDir);
                }
            }
            catch { }

            candidateDirs.Add(AppContext.BaseDirectory);
            candidateDirs.Add(AppDomain.CurrentDomain.BaseDirectory);

            foreach (var startDir in candidateDirs.Distinct())
            {
                var current = new DirectoryInfo(startDir);
                for (int i = 0; i < 8 && current != null; i++)
                {
                    // Anchor 1: JumperTools.sln in root
                    if (File.Exists(Path.Combine(current.FullName, "JumperTools.sln")))
                    {
                        return current.FullName;
                    }

                    // Anchor 2: Both tools/platform-tools and database directories coexist
                    if (Directory.Exists(Path.Combine(current.FullName, "tools", "platform-tools")) &&
                        Directory.Exists(Path.Combine(current.FullName, "database")))
                    {
                        return current.FullName;
                    }

                    // Anchor 3: database directory exists with firehose or sqlite
                    if (Directory.Exists(Path.Combine(current.FullName, "database", "firehose")))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            // Fallback: If not found, return BaseDirectory
            return AppContext.BaseDirectory;
        }

        /// <summary>
        /// Ensure common operational working directories exist.
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(BackupsDirectory)) Directory.CreateDirectory(BackupsDirectory);
            if (!Directory.Exists(LogsDirectory)) Directory.CreateDirectory(LogsDirectory);
            if (!Directory.Exists(ReceiptsDirectory)) Directory.CreateDirectory(ReceiptsDirectory);
            if (!Directory.Exists(ExtractedDirectory)) Directory.CreateDirectory(ExtractedDirectory);
        }
    }
}
