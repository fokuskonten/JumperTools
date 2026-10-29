using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Core
{
    public class ServerAuthResponse
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ServerMessage { get; set; } = string.Empty;
        public int RemainingCredits { get; set; }
    }

    /// <summary>
    /// Hybrid Server Auth Gateway: Connects to remote authorized servers for modern 2024-2026 SLA/DA challenge tokens
    /// </summary>
    public class ServerAuthGatewayEngine
    {
        private readonly HttpClient _httpClient;
        public event Action<string>? OnLog;

        public string ServerEndpoint { get; set; } = "https://auth.fokuskonten.com/api/jumptools/v1/auth";
        public string ApiKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = false;

        public ServerAuthGatewayEngine(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        /// <summary>
        /// Request an online SLA/DA challenge signature from the remote auth gateway
        /// </summary>
        public async Task<ServerAuthResponse> RequestAuthSignatureAsync(
            string brand, 
            string modelName, 
            string challengeHex, 
            CancellationToken ct = default)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(ApiKey))
            {
                Log("[Auth Gateway] Mode Server Online tidak aktif. Menggunakan metode exploit / BROM SLA offline lokal.");
                return new ServerAuthResponse
                {
                    Success = false,
                    ServerMessage = "Gateway Online tidak diaktifkan. Gunakan Offline TestPoint / BROM Bypass."
                };
            }

            Log($"[Auth Gateway] Mengirim challenge signature ({brand} {modelName}) ke gateway server...");

            try
            {
                var payload = new
                {
                    brand = brand,
                    model = modelName,
                    challenge = challengeHex,
                    api_key = ApiKey,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(ServerEndpoint, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<ServerAuthResponse>(responseBody);
                    Log($"[Auth Gateway] Otorisasi Server Sukses! Sisa Kredit: {result?.RemainingCredits ?? 0}");
                    return result ?? new ServerAuthResponse { Success = true, ServerMessage = "OK" };
                }
                else
                {
                    var err = $"Server merespons kode {response.StatusCode}";
                    Log($"[Auth Gateway Error] {err}");
                    return new ServerAuthResponse { Success = false, ServerMessage = err };
                }
            }
            catch (Exception ex)
            {
                Log($"[Auth Gateway Exception] {ex.Message}");
                return new ServerAuthResponse { Success = false, ServerMessage = ex.Message };
            }
        }
    }
}
