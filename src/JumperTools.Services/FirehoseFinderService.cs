using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Services
{
    public class FirehoseFinderService
    {
        private readonly string _databasePath;
        private readonly string _fhLoaderPath;
        private readonly string _saharaPath;

        public FirehoseFinderService(string? databasePath = null, string? fhLoaderPath = null, string? saharaPath = null)
        {
            _databasePath = ResolvePath(databasePath, "database", "firehose", "ForFilter.xml");
            _fhLoaderPath = ResolvePath(fhLoaderPath, "tools", "qualcomm", "firehose-finder", "fh_loader.exe");
            _saharaPath = ResolvePath(saharaPath, "tools", "qualcomm", "firehose-finder", "QSaharaServer.exe");
        }

        private static string ResolvePath(string? explicitPath, params string[] subPaths)
        {
            if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
                return explicitPath;

            var searchRoots = new List<string>();

            try
            {
                var procPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(procPath))
                {
                    var pDir = Path.GetDirectoryName(procPath);
                    if (!string.IsNullOrEmpty(pDir)) searchRoots.Add(pDir);
                }
            }
            catch { }

            searchRoots.Add(AppContext.BaseDirectory);
            searchRoots.Add(AppDomain.CurrentDomain.BaseDirectory);
            searchRoots.Add(Directory.GetCurrentDirectory());

            foreach (var root in searchRoots.Distinct())
            {
                var current = new DirectoryInfo(root);
                for (int i = 0; i < 8 && current != null; i++)
                {
                    var candidate = Path.Combine(new[] { current.FullName }.Concat(subPaths).ToArray());
                    if (File.Exists(candidate))
                        return candidate;
                    current = current.Parent;
                }
            }

            return Path.Combine(new[] { AppContext.BaseDirectory }.Concat(subPaths).ToArray());
        }

        public async Task<List<FirehoseLoaderData>> LoadDatabaseAsync()
        {
            return await Task.Run(() =>
            {
                var loaders = new List<FirehoseLoaderData>();
                if (!File.Exists(_databasePath)) return loaders;

                try
                {
                    XDocument doc = XDocument.Load(_databasePath);
                    var elements = doc.Descendants().Where(e => e.Name.LocalName.Equals("ForFilter", StringComparison.OrdinalIgnoreCase));

                    foreach (var el in elements)
                    {
                        loaders.Add(new FirehoseLoaderData
                        {
                            HWID = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HWID", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            FullName = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("FullName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            OEMID = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("OEMID", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            MODELID = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("MODELID", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            HASHID = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HASHID", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            Trademark = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Trademark", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            Model = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Model", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            AltName = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("AltName", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty,
                            Url = el.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("Url", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading Firehose database: {ex.Message}");
                }

                return loaders;
            });
        }

        public async Task<List<FirehoseLoaderData>> SearchLoadersAsync(string query, List<FirehoseLoaderData> allLoaders)
        {
            if (string.IsNullOrWhiteSpace(query))
                return allLoaders;

            var q = query.ToLowerInvariant();
            return await Task.Run(() => allLoaders.Where(x => 
                x.HWID.ToLowerInvariant().Contains(q) || 
                x.FullName.ToLowerInvariant().Contains(q) ||
                x.Trademark.ToLowerInvariant().Contains(q) ||
                x.Model.ToLowerInvariant().Contains(q) ||
                x.AltName.ToLowerInvariant().Contains(q)
            ).ToList());
        }

        public async Task<bool> SendLoaderViaSaharaAsync(string port, string loaderFilePath)
        {
            if (!File.Exists(loaderFilePath)) throw new FileNotFoundException("Loader file not found.", loaderFilePath);
            if (!File.Exists(_saharaPath)) throw new FileNotFoundException("QSaharaServer.exe not found.", _saharaPath);

            // Clean port string (e.g., "COM5" -> "5")
            string portNum = port.Replace("COM", "", StringComparison.OrdinalIgnoreCase);

            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _saharaPath,
                        Arguments = $"-p \\\\.\\COM{portNum} -s 13:\"{loaderFilePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        if (process == null) return false;
                        
                        string output = process.StandardOutput.ReadToEnd();
                        string err = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        Debug.WriteLine(output);
                        if (!string.IsNullOrEmpty(err)) Debug.WriteLine(err);

                        return process.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sending loader: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<string> GetDeviceHwidAsync(string port)
        {
            if (!File.Exists(_saharaPath)) throw new FileNotFoundException("QSaharaServer.exe not found.", _saharaPath);

            string portNum = port.Replace("COM", "", StringComparison.OrdinalIgnoreCase);

            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _saharaPath,
                        Arguments = $"-p \\\\.\\COM{portNum} -c 1",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        if (process == null) return string.Empty;
                        
                        string output = process.StandardOutput.ReadToEnd();
                        string err = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        string fullOutput = output + "\n" + err;
                        
                        // Extract hex strings (usually 16 chars like HWID or PK_HASH)
                        var match = System.Text.RegularExpressions.Regex.Match(fullOutput, @"0x([0-9a-fA-F]{16})");
                        if (match.Success)
                        {
                            return match.Groups[1].Value.ToLowerInvariant();
                        }
                        
                        // Fallback: look for any 16-char hex sequence
                        match = System.Text.RegularExpressions.Regex.Match(fullOutput, @"\b([0-9a-fA-F]{16})\b");
                        if (match.Success)
                        {
                            return match.Groups[1].Value.ToLowerInvariant();
                        }
                        
                        return string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading HWID: {ex.Message}");
                    return string.Empty;
                }
            });
        }

        public async Task<string> FindLoaderByHwidAsync(string hwid)
        {
            if (string.IsNullOrWhiteSpace(hwid)) return string.Empty;

            string baseFirehoseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "firehose");
            if (!Directory.Exists(baseFirehoseDir)) return string.Empty;

            return await Task.Run(() =>
            {
                try
                {
                    var allowedExtensions = new[] { ".mbn", ".elf", ".bin" };
                    var allFiles = Directory.EnumerateFiles(baseFirehoseDir, "*.*", SearchOption.AllDirectories)
                        .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()));

                    foreach (var file in allFiles)
                    {
                        string fileName = Path.GetFileName(file).ToLowerInvariant();
                        if (fileName.Contains(hwid.ToLowerInvariant()))
                        {
                            return file;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error scanning for loader: {ex.Message}");
                }
                
                return string.Empty;
            });
        }
    }
}
