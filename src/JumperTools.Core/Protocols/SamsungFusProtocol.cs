using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JumperTools.Core.Protocols
{
    public class SamsungFirmwareInfo
    {
        public string Model { get; set; } = string.Empty;
        public string Csc { get; set; } = string.Empty;
        public string PdaVersion { get; set; } = string.Empty;
        public string CscVersion { get; set; } = string.Empty;
        public string PhoneVersion { get; set; } = string.Empty;
        public string FullVersionString { get; set; } = string.Empty;
        public string AndroidVersion { get; set; } = string.Empty;
        public string BinaryBit { get; set; } = string.Empty;
        public string EncryptedFileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted => FileSizeBytes > 1024 * 1024 * 1024
            ? $"{FileSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
            : $"{FileSizeBytes / (1024.0 * 1024):F1} MB";
        public string LogicCheck { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Official Samsung Firmware Update Server (FUS) Client & Cryptographic Decryptor.
    /// Interacts directly with Samsung CDN (fota-cloud-dn.ospserver.net) using standard .NET cryptography.
    /// Based on verified open-source FUS research (SamFirm_Reborn, Bifrost, Samloader).
    /// </summary>
    public class SamsungFusProtocol
    {
        private readonly HttpClient _httpClient;
        private const string FUS_CHECK_URL = "https://fota-cloud-dn.ospserver.net/firmware/{0}/{1}/version.xml";
        private const string FUS_TEST_URL = "https://fota-cloud-dn.ospserver.net/firmware/{0}/{1}/version.test.xml";

        public event Action<string>? OnLog;

        public SamsungFusProtocol(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Kies2.0_Win");
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Samsung FUS] {message}");
        }

        /// <summary>
        /// Check latest official firmware from Samsung FUS servers
        /// </summary>
        public async Task<SamsungFirmwareInfo> CheckLatestFirmwareAsync(
            string model,
            string csc = "XID",
            string? imei = null,
            CancellationToken cancellationToken = default)
        {
            var info = new SamsungFirmwareInfo
            {
                Model = model.Trim().ToUpperInvariant(),
                Csc = string.IsNullOrWhiteSpace(csc) ? "XID" : csc.Trim().ToUpperInvariant()
            };

            Log($"Memeriksa versi firmware resmi untuk Model: {info.Model}, Wilayah CSC: {info.Csc}...");

            try
            {
                var targetUrl = string.Format(FUS_CHECK_URL, info.Csc, info.Model);
                using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                request.Headers.Add("User-Agent", "Kies2.0_Win");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // Coba fallback ke version.test.xml
                    var fallbackUrl = string.Format(FUS_TEST_URL, info.Csc, info.Model);
                    using var fallbackReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
                    fallbackReq.Headers.Add("User-Agent", "Kies2.0_Win");
                    using var fallbackResp = await _httpClient.SendAsync(fallbackReq, cancellationToken);

                    if (!fallbackResp.IsSuccessStatusCode)
                    {
                        info.IsValid = false;
                        info.Message = $"Server Samsung FUS membalas HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Pastikan Model dan CSC sesuai.";
                        Log($"[Peringatan] {info.Message}");
                        return info;
                    }
                }

                var xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(xmlContent))
                {
                    info.IsValid = false;
                    info.Message = "Response XML dari server Samsung kosong.";
                    return info;
                }

                // Parse XML Version Info
                var doc = XDocument.Parse(xmlContent);
                var firmwareElem = doc.Root?.Element("firmware");
                var versionElem = firmwareElem?.Element("version");

                if (versionElem != null)
                {
                    var latest = versionElem.Element("latest")?.Value?.Trim() ?? string.Empty;
                    var upgrade = versionElem.Element("upgrade")?.Element("value")?.Value?.Trim() ?? string.Empty;

                    info.FullVersionString = !string.IsNullOrEmpty(latest) ? latest : upgrade;

                    // Pecah versi: PDA/CSC/PHONE/PDA (Contoh: A155FXXU1AXA1/A155FOLE1AXA1/A155FXXU1AXA1/A155FXXU1AXA1)
                    var parts = info.FullVersionString.Split('/');
                    if (parts.Length >= 1) info.PdaVersion = parts[0];
                    if (parts.Length >= 2) info.CscVersion = parts[1];
                    if (parts.Length >= 3) info.PhoneVersion = parts[2];

                    // Deteksi Binary Bit (Karakter ke-5 dari belakang pada PDA string)
                    if (info.PdaVersion.Length >= 5)
                    {
                        var bitChar = info.PdaVersion[^5];
                        info.BinaryBit = $"U{bitChar} / Bit {bitChar}";
                    }

                    // Tentukan perkiraan Android version berdasarkan pola build
                    info.AndroidVersion = DetermineEstimatedAndroidVersion(info.PdaVersion);

                    // Bentuk nama file target zip/tar
                    info.EncryptedFileName = $"{info.Model}_{info.Csc}_{info.PdaVersion}.zip.enc4";
                    info.LogicCheck = info.PdaVersion;
                    info.IsValid = true;
                    info.Message = "Firmware resmi berhasil ditemukan di server Samsung FUS.";

                    Log($"[Ditemukan] Versi PDA: {info.PdaVersion} | CSC: {info.CscVersion} | Binary Bit: {info.BinaryBit} | OS: {info.AndroidVersion}");
                }
                else
                {
                    info.IsValid = false;
                    info.Message = "Model/CSC tidak ditemukan di database FUS Samsung.";
                    Log($"[Gagal] {info.Message}");
                }
            }
            catch (Exception ex)
            {
                info.IsValid = false;
                info.Message = $"Gagal terhubung ke FUS: {ex.Message}";
                Log($"[Error] {info.Message}");
            }

            return info;
        }

        /// <summary>
        /// Decrypts Samsung .enc4 / .enc2 encrypted file stream to standard .tar.md5 / .zip archive using AES-128/256-ECB.
        /// Streams in 64 KB chunks to preserve RAM.
        /// </summary>
        public async Task<bool> DecryptEncryptedFirmwareStreamAsync(
            Stream encryptedStream,
            Stream decryptedOutputStream,
            string logicValue,
            string model,
            string region,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Log("Memulai proses dekripsi biner firmware resmi Samsung (AES-128-ECB)...");

            try
            {
                // Generate Samsung AES Key from Logic, Model, Region
                var key = DeriveSamsungFusKey(logicValue, model, region);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                using var cryptoStream = new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read);

                var buffer = new byte[64 * 1024]; // 64 KB chunk
                long totalBytes = encryptedStream.Length;
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await cryptoStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await decryptedOutputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var pct = Math.Min(100.0, (totalRead / (double)totalBytes) * 100.0);
                        progress.Report(pct);
                    }
                }

                await decryptedOutputStream.FlushAsync(cancellationToken);
                Log("[Sukses] Firmware berhasil didekripsi menjadi file flashable (.tar.md5 / .zip)!");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[Decrypt Error] Gagal mendekripsi file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Key derivation function based on Samsung KiesAuth protocol (MD5 hash of combined logic seeds)
        /// </summary>
        public static byte[] DeriveSamsungFusKey(string logicValue, string model, string region)
        {
            var seed = $"{logicValue}:{region}:{model}";
            using var md5 = MD5.Create();
            return md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        private static string DetermineEstimatedAndroidVersion(string pda)
        {
            if (string.IsNullOrEmpty(pda)) return "Android Official";
            var upper = pda.ToUpperInvariant();
            if (upper.Contains("AX") || upper.Contains("BX") || upper.Contains("CX")) return "Android 14 / One UI 6";
            if (upper.Contains("AW") || upper.Contains("BW") || upper.Contains("CW")) return "Android 13 / One UI 5";
            if (upper.Contains("AV") || upper.Contains("BV") || upper.Contains("CV")) return "Android 12 / One UI 4";
            if (upper.Contains("AU") || upper.Contains("BU")) return "Android 11 / One UI 3";
            return "Android Official (Multi-Version)";
        }
    }
}
