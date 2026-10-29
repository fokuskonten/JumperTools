using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using JumperTools.Models;

namespace JumperTools.Services
{
    /// <summary>
    /// API client for communicating with FokusKonten server
    /// </summary>
    public class JumperToolsApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private string? _jwtToken;
        private DateTime? _tokenExpiry;

        public JumperToolsApiClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// Check if master backend server is reachable
        /// </summary>
        public async Task<bool> CheckServerHealthAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await _httpClient.GetAsync("/", cts.Token);
                return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resilient HTTP sender with exponential backoff for network jitter
        /// </summary>
        private async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<Task<HttpResponseMessage>> requestAction,
            int maxRetries = 2,
            int delayMs = 500)
        {
            Exception? lastEx = null;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await requestAction();
                    if ((int)response.StatusCode < 500)
                    {
                        return response; // 2xx, 3xx, 4xx are final
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastEx = ex;
                }
                catch (TaskCanceledException ex)
                {
                    lastEx = ex;
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(delayMs * (attempt + 1));
                }
            }

            if (lastEx != null) throw lastEx;
            throw new HttpRequestException("Server error after retries");
        }

        /// <summary>
        /// Login to FokusKonten server
        /// </summary>
        public async Task<LoginResult> LoginAsync(string email, string password)
        {
            var loginData = new
            {
                email = email,
                password = password
            };

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(loginData), Encoding.UTF8, "application/json");
                var response = await SendWithRetryAsync(() => _httpClient.PostAsync("/api/auth/login", content));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new LoginResult { Success = false, ErrorMessage = error };
                }

                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (result == null)
                {
                    return new LoginResult { Success = false, ErrorMessage = "Invalid response from server" };
                }

                _jwtToken = result.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddHours(1); // Default 1 hour expiry

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _jwtToken);

                return new LoginResult
                {
                    Success = true,
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken,
                    User = result.User
                };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Validate license for HP Tools
        /// </summary>
        public async Task<LicenseValidationResult> ValidateLicenseAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Not authenticated"
                };
            }

            var licenseData = new
            {
                device_id = deviceId
            };

            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(licenseData), Encoding.UTF8, "application/json");
                var response = await SendWithRetryAsync(() => _httpClient.PostAsync("/api/hp-tools/auth/validate-license", content));

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = error
                    };
                }

                var result = await response.Content.ReadFromJsonAsync<LicenseValidationResponse>();
                if (result == null)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Invalid response from server"
                    };
                }

                return new LicenseValidationResult
                {
                    IsValid = result.Valid,
                    ErrorMessage = result.Error,
                    License = result.License != null ? MapToLicense(result.License) : null,
                    AvailableFeatures = result.Features ?? Array.Empty<string>()
                };
            }
            catch (Exception ex)
            {
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Get device compatibility info from server
        /// </summary>
        public async Task<DeviceCompatibility?> GetDeviceCompatibilityAsync(string model)
        {
            if (string.IsNullOrEmpty(_jwtToken))
                return null;

            var response = await _httpClient.GetAsync($"/api/hp-tools/devices/compatibility/{model}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<DeviceCompatibility>();
        }

        /// <summary>
        /// Get ROM list for a device model
        /// </summary>
        public async Task<List<RomMetadata>> GetRomListAsync(string model)
        {
            if (string.IsNullOrEmpty(_jwtToken))
                return new List<RomMetadata>();

            var response = await _httpClient.GetAsync($"/api/hp-tools/roms/list/{model}");

            if (!response.IsSuccessStatusCode)
                return new List<RomMetadata>();

            var result = await response.Content.ReadFromJsonAsync<List<RomMetadata>>();
            return result ?? new List<RomMetadata>();
        }

        /// <summary>
        /// Get Test Point & ISP pinout diagrams from server
        /// </summary>
        public async Task<List<TestPointInfo>> GetTestPointsAsync(string? brand = null)
        {
            var url = string.IsNullOrEmpty(brand) ? "/api/hp-tools/testpoints" : $"/api/hp-tools/testpoints?brand={Uri.EscapeDataString(brand)}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<TestPointInfo>();

                var result = await response.Content.ReadFromJsonAsync<List<TestPointInfo>>();
                return result ?? new List<TestPointInfo>();
            }
            catch
            {
                return new List<TestPointInfo>();
            }
        }

        /// <summary>
        /// Get available Firehose loaders from cloud repository
        /// </summary>
        public async Task<List<string>> GetFirehoseLoadersAsync(string chipset)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/hp-tools/loaders/{chipset}");
                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var result = await response.Content.ReadFromJsonAsync<List<string>>();
                return result ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Get full loader catalog from cloud repository
        /// </summary>
        public async Task<List<LoaderInfo>> GetLoadersCatalogAsync(string? brand = null, string? chipset = null)
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(brand)) query.Add($"brand={Uri.EscapeDataString(brand)}");
            if (!string.IsNullOrEmpty(chipset)) query.Add($"chipset={Uri.EscapeDataString(chipset)}");
            var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";

            try
            {
                var response = await _httpClient.GetAsync($"/api/hp-tools/loaders{queryString}");
                if (!response.IsSuccessStatusCode)
                    return new List<LoaderInfo>();

                var result = await response.Content.ReadFromJsonAsync<List<LoaderInfo>>();
                return result ?? new List<LoaderInfo>();
            }
            catch
            {
                return new List<LoaderInfo>();
            }
        }

        /// <summary>
        /// Download loader file on-demand to local cache path
        /// </summary>
        public async Task<bool> DownloadLoaderFileAsync(string fileName, string savePath)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/hp-tools/loaders/download/{Uri.EscapeDataString(fileName)}");
                if (!response.IsSuccessStatusCode)
                    return false;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(savePath, bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Log operation to server
        /// </summary>
        public async Task<bool> LogOperationAsync(OperationLog log)
        {
            if (string.IsNullOrEmpty(_jwtToken))
                return false;

            var content = new StringContent(JsonConvert.SerializeObject(log), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/hp-tools/analytics/log-operation", content);

            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Get user settings from server
        /// </summary>
        public async Task<UserSettings?> GetUserSettingsAsync()
        {
            if (string.IsNullOrEmpty(_jwtToken))
                return null;

            var response = await _httpClient.GetAsync("/api/hp-tools/config/user-settings");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<UserSettings>();
        }

        /// <summary>
        /// Update user settings on server
        /// </summary>
        public async Task<bool> UpdateUserSettingsAsync(UserSettings settings)
        {
            if (string.IsNullOrEmpty(_jwtToken))
                return false;

            var content = new StringContent(JsonConvert.SerializeObject(settings), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync("/api/hp-tools/config/user-settings", content);

            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Check if token needs refresh
        /// </summary>
        public bool NeedsTokenRefresh()
        {
            return _tokenExpiry.HasValue && DateTime.UtcNow >= _tokenExpiry.Value.AddMinutes(-10);
        }

        /// <summary>
        /// Refresh JWT token
        /// </summary>
        public async Task<bool> RefreshTokenAsync(string refreshToken)
        {
            var refreshData = new
            {
                refresh_token = refreshToken
            };

            var content = new StringContent(JsonConvert.SerializeObject(refreshData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/auth/refresh", content);

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result == null)
                return false;

            _jwtToken = result.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddHours(1);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _jwtToken);

            return true;
        }

        /// <summary>
        /// Logout
        /// </summary>
        public async Task LogoutAsync()
        {
            if (!string.IsNullOrEmpty(_jwtToken))
            {
                await _httpClient.PostAsync("/api/auth/logout", null);
            }

            _jwtToken = null;
            _tokenExpiry = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        private License MapToLicense(LicenseDto dto)
        {
            return new License
            {
                Id = dto.Id,
                UserId = dto.UserId,
                DeviceId = dto.DeviceId,
                Type = Enum.Parse<LicenseType>(dto.Type),
                Features = dto.Features ?? Array.Empty<string>(),
                ExpiryDate = DateTime.Parse(dto.ExpiryDate),
                IsActive = dto.IsActive,
                MaxDevices = dto.MaxDevices,
                CurrentDevices = dto.CurrentDevices,
                LastValidated = DateTime.TryParse(dto.LastValidated, out var lastValidated) ? lastValidated : null
            };
        }
    }

    // DTOs for API responses
    public class LoginResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public UserInfo? User { get; set; }
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public UserInfo User { get; set; } = new();
    }

    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class LicenseValidationResponse
    {
        public bool Valid { get; set; }
        public string? Error { get; set; }
        public LicenseDto? License { get; set; }
        public string[]? Features { get; set; }
    }

    public class LicenseDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string[]? Features { get; set; }
        public string ExpiryDate { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int MaxDevices { get; set; }
        public int CurrentDevices { get; set; }
        public string? LastValidated { get; set; }
    }

    public class DeviceCompatibility
    {
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string[] SupportedOperations { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> KnownIssues { get; set; } = new();
        public Dictionary<string, double> SuccessRate { get; set; } = new();
    }

    public class OperationLog
    {
        public string DeviceId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string ToolUsed { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int DurationMs { get; set; }
        public Dictionary<string, string> DeviceFingerprint { get; set; } = new();
    }

    public class UserSettings
    {
        public Dictionary<string, object> Settings { get; set; } = new();
        public DateTime LastSync { get; set; }
    }
}
