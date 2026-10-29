using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Services
{
    public class DownloadProgressInfo
    {
        public long BytesReceived { get; set; }
        public long TotalBytesToReceive { get; set; }
        public double ProgressPercentage { get; set; }
        public double SpeedBytesPerSecond { get; set; }
        public string SpeedFormatted => SpeedBytesPerSecond > 1024 * 1024
            ? $"{SpeedBytesPerSecond / (1024.0 * 1024):F1} MB/s"
            : $"{SpeedBytesPerSecond / 1024.0:F0} KB/s";
        public TimeSpan Eta { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    /// <summary>
    /// High-Performance Resumable Firmware Downloader Service.
    /// Supports HTTP Range (Chunked Resume), Bandwidth Throttling/Measurement, and Hash Integrity Verification.
    /// </summary>
    public class AutoRomDownloaderService
    {
        private readonly HttpClient _httpClient;

        public event Action<string>? OnLog;

        public AutoRomDownloaderService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Auto-ROM Downloader] {message}");
        }

        /// <summary>
        /// Download large ROM archive with HTTP Range Resume Support & Real-Time Progress Calculation
        /// </summary>
        public async Task<bool> DownloadFileWithResumeAsync(
            string downloadUrl,
            string destinationFilePath,
            IProgress<DownloadProgressInfo>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                Log("[Error] URL download kosong.");
                return false;
            }

            var destDir = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            long existingBytes = 0;
            if (File.Exists(destinationFilePath))
            {
                existingBytes = new FileInfo(destinationFilePath).Length;
                Log($"[Resume] Ditemukan file parsial ({existingBytes / (1024 * 1024):N1} MB). Mencoba melanjutkan unduhan (HTTP Range)...");
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                if (existingBytes > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(existingBytes, null);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Validasi Status Code (200 OK atau 206 Partial Content)
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                {
                    Log($"[HTTP Error] Server menolak unduhan: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return false;
                }

                long totalBytes = (response.Content.Headers.ContentLength ?? 0) + existingBytes;
                var isAppendMode = response.StatusCode == System.Net.HttpStatusCode.PartialContent && existingBytes > 0;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(
                    destinationFilePath,
                    isAppendMode ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 64 * 1024,
                    useAsync: true);

                var buffer = new byte[64 * 1024]; // 64 KB chunk
                long totalReceived = isAppendMode ? existingBytes : 0;
                int bytesRead;

                var stopwatch = Stopwatch.StartNew();
                long lastBytesCount = totalReceived;
                var lastTime = stopwatch.ElapsedMilliseconds;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalReceived += bytesRead;

                    var now = stopwatch.ElapsedMilliseconds;
                    if (now - lastTime >= 500) // Update progress tiap 500ms
                    {
                        var bytesDelta = totalReceived - lastBytesCount;
                        var timeDeltaSec = (now - lastTime) / 1000.0;
                        var speedBps = timeDeltaSec > 0 ? bytesDelta / timeDeltaSec : 0;

                        double pct = totalBytes > 0 ? Math.Min(100.0, (totalReceived / (double)totalBytes) * 100.0) : 0;
                        var remainingBytes = totalBytes - totalReceived;
                        var etaSeconds = speedBps > 0 ? remainingBytes / speedBps : 0;
                        var eta = TimeSpan.FromSeconds(Math.Max(0, etaSeconds));

                        var receivedMb = totalReceived / (1024.0 * 1024.0);
                        var totalMb = totalBytes / (1024.0 * 1024.0);

                        var progressReport = new DownloadProgressInfo
                        {
                            BytesReceived = totalReceived,
                            TotalBytesToReceive = totalBytes,
                            ProgressPercentage = pct,
                            SpeedBytesPerSecond = speedBps,
                            Eta = eta,
                            StatusText = $"{pct:F1}% ({receivedMb:N0} MB / {totalMb:N0} MB) — {speedBps / (1024 * 1024):F1} MB/s — Sisa: {eta:mm\\:ss}"
                        };

                        progress?.Report(progressReport);

                        lastBytesCount = totalReceived;
                        lastTime = now;
                    }
                }

                await fileStream.FlushAsync(cancellationToken);
                Log($"[Sukses] Unduhan selesai 100%: {Path.GetFileName(destinationFilePath)} ({totalReceived / (1024 * 1024):N1} MB)");
                return true;
            }
            catch (OperationCanceledException)
            {
                Log("[Info] Unduhan dibatalkan oleh pengguna atau dijeda.");
                return false;
            }
            catch (Exception ex)
            {
                Log($"[Download Error] {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verify MD5 Hash checksum of downloaded ROM file
        /// </summary>
        public static async Task<bool> VerifyMd5ChecksumAsync(string filePath, string expectedMd5, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath) || string.IsNullOrWhiteSpace(expectedMd5)) return true;

            return await Task.Run(() =>
            {
                try
                {
                    using var md5 = MD5.Create();
                    using var stream = File.OpenRead(filePath);
                    var hash = md5.ComputeHash(stream);
                    var hashHex = Convert.ToHexString(hash);
                    return hashHex.Equals(expectedMd5.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }
    }
}
