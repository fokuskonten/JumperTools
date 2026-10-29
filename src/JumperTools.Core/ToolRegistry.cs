using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JumperTools.Models;
using JumperTools.Services;

namespace JumperTools.Core
{
    public enum ToolStatus
    {
        Ready,
        Missing,
        Corrupted,
        UnsupportedVersion
    }

    public class ToolItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // PlatformTools, MediaTek, Qualcomm, Odin, Unisoc, Apple
        public string RelativePath { get; set; } = string.Empty;
        public string? ExpectedSha256 { get; set; }
        public string MinVersion { get; set; } = "1.0";
        public ToolStatus Status { get; set; } = ToolStatus.Missing;
        public string ResolvedFullPath { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Managed registry for external binaries with path resolution, fallback ladders, and integrity auditing
    /// </summary>
    public class ToolRegistry
    {
        private readonly string _toolsBasePath;
        private readonly BackupIntegrityService _integrityService;
        private readonly Dictionary<string, ToolItem> _registeredTools = new(StringComparer.OrdinalIgnoreCase);

        public ToolRegistry(string? toolsBasePath = null, BackupIntegrityService? integrityService = null)
        {
            _toolsBasePath = !string.IsNullOrEmpty(toolsBasePath) && Directory.Exists(toolsBasePath)
                ? toolsBasePath
                : AppPathResolver.ToolsDirectory;
            _integrityService = integrityService ?? new BackupIntegrityService();
            RegisterStandardTools();
        }

        public string ToolsBasePath => _toolsBasePath;

        private void RegisterStandardTools()
        {
            RegisterTool(new ToolItem { Name = "adb", Category = "PlatformTools", RelativePath = "platform-tools/adb.exe", MinVersion = "30.0" });
            RegisterTool(new ToolItem { Name = "fastboot", Category = "PlatformTools", RelativePath = "platform-tools/fastboot.exe", MinVersion = "30.0" });
            RegisterTool(new ToolItem { Name = "mtkclient", Category = "MediaTek", RelativePath = "mtkclient/mtk.py" });
            RegisterTool(new ToolItem { Name = "qcom-edl", Category = "Qualcomm", RelativePath = "qcom-edl/edl.py" });
            RegisterTool(new ToolItem { Name = "odin", Category = "Samsung", RelativePath = "odin/Odin3.exe" });
            RegisterTool(new ToolItem { Name = "spd-research", Category = "Unisoc", RelativePath = "spd/ResearchDownload.exe" });
            RegisterTool(new ToolItem { Name = "spd-upgrade", Category = "Unisoc", RelativePath = "spd/UpgradeDownload.exe" });
            RegisterTool(new ToolItem { Name = "ideviceinfo", Category = "Apple", RelativePath = "apple/libimobiledevice/ideviceinfo.exe" });
            RegisterTool(new ToolItem { Name = "irecovery", Category = "Apple", RelativePath = "apple/libimobiledevice/irecovery.exe" });
            RegisterTool(new ToolItem { Name = "miflash", Category = "Xiaomi", RelativePath = "xiaomi/MiFlash.exe" });
            RegisterTool(new ToolItem { Name = "qsaharaserver", Category = "Qualcomm", RelativePath = "qualcomm/firehose-finder/QSaharaServer.exe" });
            RegisterTool(new ToolItem { Name = "fh_loader", Category = "Qualcomm", RelativePath = "qualcomm/firehose-finder/fh_loader.exe" });
            RegisterTool(new ToolItem { Name = "python", Category = "Runtime", RelativePath = "runtime/python/python.exe" });
        }

        public void RegisterTool(ToolItem item)
        {
            _registeredTools[item.Name] = item;
            ResolveToolPath(item);
        }

        public ToolItem? GetTool(string name)
        {
            if (_registeredTools.TryGetValue(name, out var item))
            {
                ResolveToolPath(item);
                return item;
            }
            return null;
        }

        public bool IsToolReady(string name)
        {
            var tool = GetTool(name);
            return tool != null && tool.Status == ToolStatus.Ready && File.Exists(tool.ResolvedFullPath);
        }

        public string? GetToolPath(string name)
        {
            var tool = GetTool(name);
            return tool?.Status == ToolStatus.Ready ? tool.ResolvedFullPath : null;
        }

        private void ResolveToolPath(ToolItem tool)
        {
            // 1. Direct path inside _toolsBasePath takes absolute priority
            var directPath = Path.Combine(_toolsBasePath, tool.RelativePath);
            if (File.Exists(directPath))
            {
                tool.ResolvedFullPath = Path.GetFullPath(directPath);
                tool.Status = ToolStatus.Ready;
                tool.ErrorMessage = null;
                return;
            }

            // Fallback ladder:
            var candidates = new List<string>
            {
                Path.Combine(AppPathResolver.ToolsDirectory, tool.RelativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", tool.RelativePath),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "tools", tool.RelativePath)
            };

            var fileName = Path.GetFileName(tool.RelativePath);

            // Category specific fallbacks
            if (tool.Category == "PlatformTools")
            {
                candidates.Add(Path.Combine(@"C:\adb", fileName));
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                candidates.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", fileName));
            }
            else if (tool.Category == "Apple")
            {
                candidates.Add(Path.Combine(_toolsBasePath, "apple", fileName));
                candidates.Add(Path.Combine(_toolsBasePath, "apple", "libimobiledevice", fileName));
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "apple", fileName));
            }
            else if (tool.Category == "Samsung")
            {
                candidates.Add(Path.Combine(_toolsBasePath, "samsung", "odin.exe"));
                candidates.Add(Path.Combine(_toolsBasePath, "odin", "Odin3.exe"));
                candidates.Add(Path.Combine(_toolsBasePath, "samsung", fileName));
            }
            else if (tool.Category == "MediaTek")
            {
                candidates.Add(Path.Combine(_toolsBasePath, "mtkclient", "mtk.py"));
                candidates.Add(Path.Combine(_toolsBasePath, "mtkclient", "mtk.exe"));
                candidates.Add(Path.Combine(_toolsBasePath, "mtkclient", "mtk.bat"));
            }
            else if (tool.Category == "Qualcomm")
            {
                candidates.Add(Path.Combine(_toolsBasePath, "qcom-edl", "edl.py"));
                candidates.Add(Path.Combine(_toolsBasePath, "qcom-edl", "edl.exe"));
                candidates.Add(Path.Combine(_toolsBasePath, "qcom-edl", "edl.bat"));
            }
            else if (tool.Category == "Runtime")
            {
                candidates.Add(Path.Combine(_toolsBasePath, "runtime", "python", "python.exe"));
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "runtime", "python", "python.exe"));
            }

            foreach (var path in candidates)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        tool.ResolvedFullPath = fullPath;
                        tool.Status = ToolStatus.Ready;
                        tool.ErrorMessage = null;
                        return;
                    }
                }
                catch { }
            }

            tool.ResolvedFullPath = Path.Combine(_toolsBasePath, tool.RelativePath);
            tool.Status = ToolStatus.Missing;
            tool.ErrorMessage = $"Tool [{tool.Name}] tidak ditemukan di direktori tools.";
        }

        public async Task<ToolStatus> AuditToolIntegrityAsync(string name)
        {
            var tool = GetTool(name);
            if (tool == null || !File.Exists(tool.ResolvedFullPath))
            {
                if (tool != null)
                {
                    tool.Status = ToolStatus.Missing;
                    tool.ErrorMessage = $"Tool [{tool.Name}] tidak ditemukan di sistem.";
                }
                return ToolStatus.Missing;
            }

            var fileInfo = new FileInfo(tool.ResolvedFullPath);
            if (fileInfo.Length == 0)
            {
                tool.Status = ToolStatus.Corrupted;
                tool.ErrorMessage = $"Binary [{tool.Name}] berukuran 0-byte (rusak).";
                return ToolStatus.Corrupted;
            }

            if (!string.IsNullOrEmpty(tool.ExpectedSha256))
            {
                var hash = await _integrityService.ComputeFileSha256Async(tool.ResolvedFullPath);
                if (!string.Equals(hash, tool.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    tool.Status = ToolStatus.Corrupted;
                    tool.ErrorMessage = $"Checksum hash [{tool.Name}] tidak cocok dengan manifest resmi.";
                    return ToolStatus.Corrupted;
                }
            }

            tool.Status = ToolStatus.Ready;
            tool.ErrorMessage = null;
            return ToolStatus.Ready;
        }

        public async Task<Dictionary<string, ToolStatus>> AuditAllToolsAsync()
        {
            var results = new Dictionary<string, ToolStatus>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _registeredTools.Keys)
            {
                var status = await AuditToolIntegrityAsync(key);
                results[key] = status;
            }
            return results;
        }

        public List<ToolItem> GetAllRegisteredTools()
        {
            var list = new List<ToolItem>();
            foreach (var item in _registeredTools.Values)
            {
                ResolveToolPath(item);
                list.Add(item);
            }
            return list;
        }
    }
}
