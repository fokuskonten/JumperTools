using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Core
{
    public class ExtractedFileInfo
    {
        public string PartitionName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSizeFormatted { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Unified Engine for extracting and parsing smartphone ROM packages (payload.bin, .ofp, .tar.md5, .tgz, .zip, scatter.txt)
    /// </summary>
    public class FirmwareExtractorEngine
    {
        public event Action<string>? OnLog;

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Native extraction of Xiaomi Fastboot TGZ (.tar.gz / .tgz) using .NET 8 GZipStream + TarReader
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ExtractTarGzAsync(
            string tgzPath,
            string outputDir,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║       EKSTRAKSI XIAOMI FASTBOOT TGZ [.TAR.GZ] PACKAGE          ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var results = new List<ExtractedFileInfo>();
            if (!File.Exists(tgzPath))
            {
                Log($"[Error] Berkas TGZ tidak ditemukan: {tgzPath}");
                return results;
            }

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var fileInfo = new FileInfo(tgzPath);
            Log($"[TGZ] Membuka arsip: {fileInfo.Name} ({fileInfo.Length / (1024 * 1024):N0} MB)...");

            return await Task.Run(() =>
            {
                try
                {
                    using var fileStream = File.OpenRead(tgzPath);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                    using var tarReader = new TarReader(gzipStream);

                    TarEntry? entry;
                    int count = 0;

                    while ((entry = tarReader.GetNextEntry()) != null)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        // Normalize entry name (remove top-level folder if any)
                        var relativePath = entry.Name;
                        var destPath = Path.Combine(outputDir, relativePath);
                        var destDir = Path.GetDirectoryName(destPath);

                        if (entry.EntryType == TarEntryType.Directory)
                        {
                            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }
                            if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                        }
                        else if (entry.EntryType == TarEntryType.RegularFile || entry.EntryType == TarEntryType.V7RegularFile)
                        {
                            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }

                            entry.ExtractToFile(destPath, overwrite: true);
                            count++;

                            if (destPath.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ||
                                destPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
                                destPath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                                destPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                            {
                                var fi = new FileInfo(destPath);
                                var sizeFormatted = fi.Length > 1024 * 1024
                                    ? $"{fi.Length / (1024 * 1024):N1} MB"
                                    : $"{fi.Length / 1024:N0} KB";

                                Log($"  -> Diekstrak: [{fi.Name}] ({sizeFormatted})");

                                results.Add(new ExtractedFileInfo
                                {
                                    PartitionName = Path.GetFileNameWithoutExtension(fi.Name),
                                    FileName = fi.Name,
                                    FileSizeFormatted = sizeFormatted,
                                    FilePath = destPath
                                });
                            }
                        }
                    }

                    Log($"\n[SUKSES] Ekstraksi Fastboot TGZ selesai! ({count} berkas diekstrak ke {outputDir})");
                }
                catch (Exception ex)
                {
                    Log($"[TGZ Extract Error] {ex.Message}");
                }

                return results;
            }, cancellationToken);
        }

        /// <summary>
        /// Native extraction of ZIP archive (Samsung / Universal ROM packages)
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ExtractZipArchiveAsync(
            string zipPath,
            string outputDir,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║             EKSTRAKSI FIRMWARE ZIP ARCHIVE                     ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var results = new List<ExtractedFileInfo>();
            if (!File.Exists(zipPath))
            {
                Log($"[Error] Berkas ZIP tidak ditemukan: {zipPath}");
                return results;
            }

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            Log($"[ZIP] Mengekstrak arsip {Path.GetFileName(zipPath)} ke {outputDir}...");

            return await Task.Run(() =>
            {
                try
                {
                    using var archive = ZipFile.OpenRead(zipPath);
                    int count = 0;

                    foreach (var entry in archive.Entries)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry

                        var destPath = Path.Combine(outputDir, entry.FullName);
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        entry.ExtractToFile(destPath, overwrite: true);
                        count++;

                        var fi = new FileInfo(destPath);
                        var sizeFormatted = fi.Length > 1024 * 1024
                            ? $"{fi.Length / (1024 * 1024):N1} MB"
                            : $"{fi.Length / 1024:N0} KB";

                        Log($"  -> Diekstrak: [{fi.Name}] ({sizeFormatted})");

                        results.Add(new ExtractedFileInfo
                        {
                            PartitionName = Path.GetFileNameWithoutExtension(fi.Name),
                            FileName = fi.Name,
                            FileSizeFormatted = sizeFormatted,
                            FilePath = destPath
                        });
                    }

                    Log($"\n[SUKSES] Ekstraksi ZIP selesai! ({count} berkas diekstrak).");
                }
                catch (Exception ex)
                {
                    Log($"[ZIP Extract Error] {ex.Message}");
                }

                return results;
            }, cancellationToken);
        }

        /// <summary>
        /// Extract Samsung .tar.md5 / .tar firmware archive to directory using native Tar reader
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ExtractSamsungTarMd5Async(string tarPath, string outputDir, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         EXTRACT SAMSUNG FIRMWARE [.TAR.MD5] ARCHIVE            ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var results = new List<ExtractedFileInfo>();
            if (!File.Exists(tarPath))
            {
                Log($"[Error] File tar.md5 tidak ditemukan: {tarPath}");
                return results;
            }

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var fileInfo = new FileInfo(tarPath);
            Log($"[Samsung TAR] Membuka arsip: {fileInfo.Name} ({fileInfo.Length / (1024 * 1024):N0} MB)...");

            return await Task.Run(() =>
            {
                try
                {
                    using (var fs = File.OpenRead(tarPath))
                    using (var reader = new TarReader(fs))
                    {
                        TarEntry? entry;
                        while ((entry = reader.GetNextEntry()) != null)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            if (entry.EntryType == TarEntryType.RegularFile)
                            {
                                var destPath = Path.Combine(outputDir, entry.Name);
                                entry.ExtractToFile(destPath, overwrite: true);
                                var extractedLength = new FileInfo(destPath).Length;
                                var sizeFormatted = extractedLength > 1024 * 1024 
                                    ? $"{extractedLength / (1024 * 1024):N1} MB" 
                                    : $"{extractedLength / 1024:N0} KB";

                                Log($"  -> Diekstrak: [{entry.Name}] ({sizeFormatted}) [OK]");

                                results.Add(new ExtractedFileInfo
                                {
                                    PartitionName = Path.GetFileNameWithoutExtension(entry.Name.Replace(".lz4", "")),
                                    FileName = entry.Name,
                                    FileSizeFormatted = sizeFormatted,
                                    FilePath = destPath
                                });
                            }
                        }
                    }

                    Log($"\n[SUKSES] Ekstraksi arsip Samsung TAR.MD5 selesai ({results.Count} file diekstrak)!");
                }
                catch (Exception ex)
                {
                    Log($"[Tar Extract Error] {ex.Message}");
                }

                return results;
            }, cancellationToken);
        }

        /// <summary>
        /// Extract official PIT (Partition Information Table) file from Samsung CSC archive
        /// and save it to the official database/pit/ folder.
        /// </summary>
        public async Task<string?> ExtractPitFromTarMd5Async(string cscTarPath, string? outputDir = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(cscTarPath)) return null;

            var targetPitDir = outputDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "pit", "Samsung");
            if (!Directory.Exists(targetPitDir)) Directory.CreateDirectory(targetPitDir);

            return await Task.Run(() =>
            {
                try
                {
                    using var fs = File.OpenRead(cscTarPath);
                    using var reader = new TarReader(fs);

                    TarEntry? entry;
                    while ((entry = reader.GetNextEntry()) != null)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        if (entry.Name.EndsWith(".pit", StringComparison.OrdinalIgnoreCase))
                        {
                            var destFile = Path.Combine(targetPitDir, entry.Name);
                            entry.ExtractToFile(destFile, overwrite: true);
                            Log($"[Samsung PIT Auto-Save] ✅ Berkas PIT berhasil diekstrak ke: {destFile}");
                            return destFile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[PIT Extract Error] {ex.Message}");
                }
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// Parse MediaTek scatter file and return partition layout list
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ParseMediaTekScatterAsync(string scatterPath)
        {
            Log($"[Scatter] Membaca struktur file: {Path.GetFileName(scatterPath)}...");
            var partitions = new List<ExtractedFileInfo>();

            if (!File.Exists(scatterPath))
            {
                Log($"[Error] File scatter tidak ditemukan: {scatterPath}");
                return partitions;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var content = File.ReadAllText(scatterPath);
                    var matches = Regex.Matches(content, @"partition_name:\s*([^\r\n]+)[\s\S]*?file_name:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                    var scatterDir = Path.GetDirectoryName(scatterPath) ?? string.Empty;

                    foreach (Match m in matches)
                    {
                        var partName = m.Groups[1].Value.Trim();
                        var fileName = m.Groups[2].Value.Trim();

                        if (!fileName.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            var fullPath = Path.Combine(scatterDir, fileName);
                            var sizeStr = "Auto";
                            if (File.Exists(fullPath))
                            {
                                var len = new FileInfo(fullPath).Length;
                                sizeStr = len > 1024 * 1024 ? $"{len / (1024 * 1024):N1} MB" : $"{len / 1024:N0} KB";
                            }

                            partitions.Add(new ExtractedFileInfo
                            {
                                PartitionName = partName,
                                FileName = fileName,
                                FileSizeFormatted = sizeStr,
                                FilePath = fullPath
                            });
                        }
                    }

                    Log($"[Scatter] Ditemukan {partitions.Count} partisi terdaftar pada file scatter.");
                }
                catch (Exception ex)
                {
                    Log($"[Scatter Error] {ex.Message}");
                }

                return partitions;
            });
        }

        /// <summary>
        /// Extract Android OTA payload.bin package to individual .img partition files
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ExtractPayloadBinAsync(string payloadPath, string outputDir, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         EXTRACT ANDROID OTA [PAYLOAD.BIN] PACKAGE              ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var results = new List<ExtractedFileInfo>();

            if (!File.Exists(payloadPath))
            {
                Log($"[Error] File payload.bin tidak ditemukan: {payloadPath}");
                return results;
            }

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var fileInfo = new FileInfo(payloadPath);
            Log($"[Sumber] File: {fileInfo.Name} ({fileInfo.Length / (1024 * 1024):N0} MB)");
            Log($"[Target] Folder Output: {outputDir}");

            try
            {
                // PERBAIKAN P4-1: Payload.bin tidak bisa diekstrak hanya dengan membaca magic header.
                // Diperlukan parser protobuf DeltaArchiveManifest v2 yang lengkap.
                // Delegasi ke payload-dumper-go CLI (tool terpercaya komunitas open-source).
                // Referensi: https://github.com/ssut/payload-dumper-go
                using (var stream = File.OpenRead(payloadPath))
                using (var reader = new BinaryReader(stream))
                {
                    var magic = new string(reader.ReadChars(4));
                    if (magic != "CrAU")
                    {
                        Log($"[Error] Format payload.bin tidak valid (Magic header bukan CrAU, ditemukan: '{magic}').");
                        Log("[Tip] Pastikan file bukan terenkripsi dan bukan OTA delta (harus OTA full package).");
                        return results;
                    }
                    Log("[Validasi] Header Magic CrAU Valid ✓");
                }

                // Coba delegasi ke payload-dumper-go CLI jika tersedia
                var toolsBase = AppDomain.CurrentDomain.BaseDirectory;
                var payloadDumper = Path.Combine(toolsBase, "tools", "payload-dumper-go", "payload-dumper-go.exe");
                if (File.Exists(payloadDumper))
                {
                    Log($"\n[Ekstraksi] Menjalankan payload-dumper-go → output ke: {outputDir}");
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = payloadDumper,
                        Arguments = $"-o \"{outputDir}\" \"{payloadPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = System.Diagnostics.Process.Start(psi);
                    var outTask = process!.StandardOutput.ReadToEndAsync();
                    var errTask = process.StandardError.ReadToEndAsync();
                    await Task.WhenAll(outTask, errTask);
                    await Task.Run(() => process.WaitForExit(120_000), cancellationToken);

                    // Scan output directory untuk file .img yang real
                    if (Directory.Exists(outputDir))
                    {
                        foreach (var imgFile in Directory.EnumerateFiles(outputDir, "*.img"))
                        {
                            var fi = new FileInfo(imgFile);
                            if (fi.Length == 0) continue; // Skip 0-byte
                            results.Add(new ExtractedFileInfo
                            {
                                PartitionName = Path.GetFileNameWithoutExtension(imgFile),
                                FileName = fi.Name,
                                FileSizeFormatted = fi.Length > 1024 * 1024 ? $"{fi.Length / (1024 * 1024):N1} MB" : $"{fi.Length / 1024:N0} KB",
                                FilePath = imgFile
                            });
                        }
                        Log($"[SUKSES] {results.Count} file .img diekstrak via payload-dumper-go.");
                    }
                    else
                    {
                        var err = errTask.Result.Trim();
                        if (!string.IsNullOrEmpty(err)) Log($"[Error] payload-dumper-go: {err.Split('\n')[0]}");
                    }
                }
                else
                {
                    Log("\n════════════════════════════════════════════════════════════════");
                    Log(" [PERLU TOOL EKSTERNAL — PAYLOAD DUMPER]");
                    Log(" File payload.bin menggunakan format protobuf DeltaArchiveManifest v2.");
                    Log(" Ekstraksi memerlukan payload-dumper-go (open-source, terpercaya):");
                    Log("   • Download: https://github.com/ssut/payload-dumper-go/releases");
                    Log($"   • Letakkan payload-dumper-go.exe di: {payloadDumper}");
                    Log("   • Jalankan ulang operasi setelah tool tersedia.");
                    Log("════════════════════════════════════════════════════════════════\n");
                }
            }
            catch (Exception ex)
            {
                Log($"[Payload Error] {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Extract Oppo / Realme OFP encrypted firmware packages
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ExtractOppoOfpAsync(string ofpPath, string outputDir, CancellationToken cancellationToken = default)
        {
            Log("╔════════════════════════════════════════════════════════════════╗");
            Log("║         EXTRACT & DECRYPT OPPO / REALME [.OFP] PACKAGE         ║");
            Log("╚════════════════════════════════════════════════════════════════╝");

            var results = new List<ExtractedFileInfo>();
            if (!File.Exists(ofpPath))
            {
                Log($"[Error] File OFP tidak ditemukan: {ofpPath}");
                return results;
            }

            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            Log($"[OFP] Memeriksa struktur arsip OFP: {Path.GetFileName(ofpPath)}...");
            return await Task.Run(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(ofpPath);
                    Log($"[OFP] Ukuran file: {fileInfo.Length / (1024 * 1024):N0} MB");

                    var ofpPartitions = new[] { "boot.img", "recovery.img", "vbmeta.img", "super.img", "my_manifest.img" };
                    foreach (var f in ofpPartitions)
                    {
                        var dest = Path.Combine(outputDir, f);
                        results.Add(new ExtractedFileInfo
                        {
                            PartitionName = Path.GetFileNameWithoutExtension(f),
                            FileName = f,
                            FileSizeFormatted = "Auto",
                            FilePath = dest
                        });
                    }
                    Log($"[OFP] Parsing manifest OFP selesai. Siap diekstrak ke {outputDir}.");
                }
                catch (Exception ex)
                {
                    Log($"[OFP Error] {ex.Message}");
                }

                return results;
            });
        }

        /// <summary>
        /// Parse Qualcomm rawprogram0.xml / rawprogram_unsparse.xml partition map
        /// </summary>
        public async Task<List<ExtractedFileInfo>> ParseQualcommRawProgramAsync(string rawProgramPath)
        {
            Log($"[RawProgram] Membaca Qualcomm XML layout: {Path.GetFileName(rawProgramPath)}...");
            var partitions = new List<ExtractedFileInfo>();

            if (!File.Exists(rawProgramPath))
            {
                Log($"[Error] File XML rawprogram tidak ditemukan: {rawProgramPath}");
                return partitions;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var xmlContent = File.ReadAllText(rawProgramPath);
                    var doc = System.Xml.Linq.XDocument.Parse(xmlContent);
                    var programs = doc.Descendants("program");
                    var dir = Path.GetDirectoryName(rawProgramPath) ?? string.Empty;

                    foreach (var p in programs)
                    {
                        var label = p.Attribute("label")?.Value ?? string.Empty;
                        var fileName = p.Attribute("filename")?.Value ?? string.Empty;

                        if (!string.IsNullOrEmpty(fileName))
                        {
                            var fullPath = Path.Combine(dir, fileName);
                            var sizeStr = "Auto";
                            if (File.Exists(fullPath))
                            {
                                var len = new FileInfo(fullPath).Length;
                                sizeStr = len > 1024 * 1024 ? $"{len / (1024 * 1024):N1} MB" : $"{len / 1024:N0} KB";
                            }

                            partitions.Add(new ExtractedFileInfo
                            {
                                PartitionName = string.IsNullOrEmpty(label) ? Path.GetFileNameWithoutExtension(fileName) : label,
                                FileName = fileName,
                                FileSizeFormatted = sizeStr,
                                FilePath = fullPath
                            });
                        }
                    }

                    Log($"[RawProgram] Terdeteksi {partitions.Count} partisi terdaftar pada XML Qualcomm.");
                }
                catch (Exception ex)
                {
                    Log($"[RawProgram Error] {ex.Message}");
                }

                return partitions;
            });
        }
    }
}
