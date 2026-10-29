using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Core.Protocols
{
    public class PacPartitionEntry
    {
        public string PartitionName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public uint DataOffset { get; set; }
        public uint DataLength { get; set; }
        public string FileSizeFormatted => DataLength > 1024 * 1024
            ? $"{DataLength / (1024.0 * 1024):F1} MB"
            : $"{DataLength / 1024.0:F0} KB";
    }

    public class PacPackageManifest
    {
        public string PackageName { get; set; } = string.Empty;
        public string ProductVersion { get; set; } = string.Empty;
        public string ProductAlias { get; set; } = string.Empty;
        public List<PacPartitionEntry> Partitions { get; set; } = new();
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Official Parser and Unpacker for Unisoc / Spreadtrum .PAC firmware packages.
    /// Parses native PAC binary headers (FDL1, FDL2, boot, recovery, system, prodnv, fixnv)
    /// </summary>
    public class UnisocPacProtocol
    {
        public event Action<string>? OnLog;

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Unisoc PAC] {message}");
        }

        /// <summary>
        /// Parse a binary .PAC firmware file and extract partition list without decompressing the whole file
        /// </summary>
        public async Task<PacPackageManifest> ParsePacHeaderAsync(string pacFilePath, CancellationToken cancellationToken = default)
        {
            var manifest = new PacPackageManifest();

            if (!File.Exists(pacFilePath))
            {
                manifest.IsValid = false;
                manifest.Message = $"File .pac tidak ditemukan: {pacFilePath}";
                return manifest;
            }

            Log($"Membaca header biner berkas Unisoc PAC: {Path.GetFileName(pacFilePath)}...");

            return await Task.Run(() =>
            {
                try
                {
                    using var fs = new FileStream(pacFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var br = new BinaryReader(fs);

                    // PAC Magic header (512 bytes header structure)
                    // Version string (WCHAR / UTF-16LE 48 bytes)
                    var versionBytes = br.ReadBytes(48);
                    manifest.ProductVersion = Encoding.Unicode.GetString(versionBytes).TrimEnd('\0');

                    // Read total files count
                    fs.Seek(500, SeekOrigin.Begin);
                    var fileCount = br.ReadInt32();

                    if (fileCount <= 0 || fileCount > 200)
                    {
                        // Fallback parsing for alternative PAC structures
                        fs.Seek(44, SeekOrigin.Begin);
                        fileCount = br.ReadInt32();
                    }

                    Log($"[Unisoc Header] Versi: {manifest.ProductVersion} | Jumlah Partisi: {fileCount}");

                    // Read File Table Entries
                    fs.Seek(512, SeekOrigin.Begin);
                    for (int i = 0; i < fileCount; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (fs.Position + 256 > fs.Length) break;

                        // Entry: Partition Name (50 bytes WCHAR), FileName (50 bytes WCHAR), Size (4 bytes), Offset (4 bytes)
                        var partNameBytes = br.ReadBytes(50);
                        var fileNameBytes = br.ReadBytes(50);
                        var size = br.ReadUInt32();
                        var offset = br.ReadUInt32();

                        var partName = Encoding.Unicode.GetString(partNameBytes).TrimEnd('\0');
                        var fileName = Encoding.Unicode.GetString(fileNameBytes).TrimEnd('\0');

                        if (!string.IsNullOrWhiteSpace(partName))
                        {
                            manifest.Partitions.Add(new PacPartitionEntry
                            {
                                PartitionName = partName,
                                FileName = string.IsNullOrWhiteSpace(fileName) ? $"{partName}.bin" : fileName,
                                DataLength = size,
                                DataOffset = offset
                            });
                        }
                    }

                    manifest.IsValid = true;
                    manifest.Message = $"Header PAC valid. ({manifest.Partitions.Count} partisi terdeteksi)";
                    Log($"[Unisoc PAC] ✅ Berhasil memetakan {manifest.Partitions.Count} partisi dari berkas .pac.");
                }
                catch (Exception ex)
                {
                    manifest.IsValid = false;
                    manifest.Message = $"Gagal membedah PAC: {ex.Message}";
                    Log($"[Error PAC] {ex.Message}");
                }

                return manifest;
            }, cancellationToken);
        }

        /// <summary>
        /// Extract all partitions from .PAC package to target directory
        /// </summary>
        public async Task<bool> ExtractPacPackageAsync(string pacFilePath, string outputDir, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var manifest = await ParsePacHeaderAsync(pacFilePath, cancellationToken);
            if (!manifest.IsValid || manifest.Partitions.Count == 0) return false;

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            Log($"[Unisoc Extract] Mengekstrak {manifest.Partitions.Count} partisi ke {outputDir}...");

            return await Task.Run(() =>
            {
                try
                {
                    using var fs = File.OpenRead(pacFilePath);
                    var buffer = new byte[1024 * 1024]; // 1MB buffer

                    for (int i = 0; i < manifest.Partitions.Count; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        var part = manifest.Partitions[i];

                        if (part.DataOffset > 0 && part.DataLength > 0 && (part.DataOffset + part.DataLength <= fs.Length))
                        {
                            fs.Seek(part.DataOffset, SeekOrigin.Begin);
                            var destPath = Path.Combine(outputDir, part.FileName);

                            using var outFs = File.Create(destPath);
                            long remaining = part.DataLength;

                            while (remaining > 0)
                            {
                                int toRead = (int)Math.Min(remaining, buffer.Length);
                                int read = fs.Read(buffer, 0, toRead);
                                if (read <= 0) break;
                                outFs.Write(buffer, 0, read);
                                remaining -= read;
                            }

                            Log($"  -> Ekstrak: [{part.PartitionName}] ({part.FileSizeFormatted}) [OK]");
                        }

                        progress?.Report((double)(i + 1) / manifest.Partitions.Count * 100);
                    }

                    Log("[Unisoc Extract] ✅ Seluruh partisi .PAC berhasil diekstrak.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"[Unisoc Extract Error] {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }
    }
}
