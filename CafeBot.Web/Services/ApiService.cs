using System.Net.Http.Json;
using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;
using CafeBot.Web.Auth;
using System.Net.Http.Headers;

namespace CafeBot.Web.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly TokenAuthenticationStateProvider _authStateProvider;

    // Grup istek deduplication: aktif istek varsa tekrar atma
    private Task<(List<GroupDto>? Data, string? Error)>? _activeGroupFetch;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger, TokenAuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authStateProvider = authStateProvider;
    }

    private async Task EnsureAuthHeader()
    {
        var token = await _authStateProvider.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<(AuthResponseDto? Data, string? Error)> LoginAsync(LoginRequestDto request)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                return (data, null);
            }
            var errorBody = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var errorMessage = errorBody != null && errorBody.ContainsKey("message") ? errorBody["message"] : "Giriş başarısız.";
            return (null, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return (null, $"Giriş yapılamadı: {ex.Message}");
        }
    }

    public async Task<(AuthResponseDto? Data, string? Error)> RegisterAsync(RegisterRequestDto request)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                return (data, null);
            }
            // Hata mesajını düzgün parse etmeyi deneriz
            var errorStr = await response.Content.ReadAsStringAsync();
            return (null, $"Kayıt başarısız: {errorStr}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register failed");
            return (null, $"Kayıt yapılamadı: {ex.Message}");
        }
    }

    public async Task<(UserProfileDto? Data, string? Error)> GetProfileAsync()
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync("api/user/profile");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<UserProfileDto>();
                return (data, null);
            }
            return (null, $"Profil alınamadı: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (null, $"Profil alınamadı: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(UpdateUserProfileDto dto)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync("api/user/profile", dto);
            if (response.IsSuccessStatusCode)
                return (true, null);
                
            return (false, $"Güncelleme başarısız: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, $"Güncelleme hatası: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAccountAsync()
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.DeleteAsync("api/user/account");
            if (response.IsSuccessStatusCode)
                return (true, null);
                
            var error = await response.Content.ReadAsStringAsync();
            return (false, $"Hesap silinemedi: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            return (false, $"Hesap silme hatası: {ex.Message}");
        }
    }

    public async Task<(QRCodeDto? Data, string? Error)> ConnectWhatsApp()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/whatsapp/connect", null);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<QRCodeDto>();
                return (data, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Bağlantı hatası: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConnectWhatsApp failed");
            return (null, $"Bağlantı kurulamadı: {ex.Message}");
        }
    }

    public async Task<(ConnectionStatus? Data, string? Error)> GetConnectionStatus()
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync("api/whatsapp/status");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ConnectionStatus>();
                return (data, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Durum alınamadı: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetConnectionStatus failed");
            return (null, $"Durum alınamadı: {ex.Message}");
        }
    }

    public async Task<(List<GroupDto>? Data, string? Error)> GetGroups(bool forceRefresh = false)
    {
        // Aktif istek varsa ve force değilse, mevcut isteği bekle (dedup)
        if (!forceRefresh && _activeGroupFetch != null && !_activeGroupFetch.IsCompleted)
        {
            return await _activeGroupFetch;
        }

        _activeGroupFetch = FetchGroupsInternal(forceRefresh);
        return await _activeGroupFetch;
    }

    private async Task<(List<GroupDto>? Data, string? Error)> FetchGroupsInternal(bool forceRefresh)
    {
        try
        {
            var url = forceRefresh ? "api/whatsapp/groups?forceRefresh=true" : "api/whatsapp/groups";
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<List<GroupDto>>();
                return (data, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Gruplar alınamadı: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroups failed");
            return (null, $"Gruplar alınamadı: {ex.Message}");
        }
    }

    public async Task<string?> UpdateGroups(List<string> groupIds, List<string> groupNames)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync("api/config/group", new { GroupIds = groupIds, GroupNames = groupNames });
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Grup kaydedilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGroups failed");
            return $"Grup kaydedilemedi: {ex.Message}";
        }
    }

    public async Task<string?> UpdateAiPrompt(string? prompt)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync("api/config/ai-prompt", new { Prompt = prompt });
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"AI komutu kaydedilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAiPrompt failed");
            return $"AI komutu kaydedilemedi: {ex.Message}";
        }
    }

    public async Task<string?> UpdatePriority(List<int> priorityList)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync("api/config/priority", priorityList);
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Öncelik listesi kaydedilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdatePriority failed");
            return $"Öncelik listesi kaydedilemedi: {ex.Message}";
        }
    }

    public async Task<(GroupSettingsDto? Data, string? Error)> GetGroupSettings(string groupId)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync($"api/config/group-settings/{groupId}");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<GroupSettingsDto>();
                return (data, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Grup ayarları alınamadı: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupSettings failed");
            return (null, $"Grup ayarları alınamadı: {ex.Message}");
        }
    }

    public async Task<string?> UpdateGroupPriority(string groupId, List<int> priorityList)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync($"api/config/group-settings/{groupId}/priority", priorityList);
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Grup öncelik listesi kaydedilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGroupPriority failed");
            return $"Grup öncelik listesi kaydedilemedi: {ex.Message}";
        }
    }

    public async Task<string?> UpdateGroupAiPrompt(string groupId, string? prompt)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync($"api/config/group-settings/{groupId}/ai-prompt", new { Prompt = prompt });
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Grup AI komutu kaydedilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGroupAiPrompt failed");
            return $"Grup AI komutu kaydedilemedi: {ex.Message}";
        }
    }

    public async Task<string?> UpdateSystemStatus(SystemStatus status)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.PutAsJsonAsync("api/config/system-status", status);
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Sistem durumu güncellenemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateSystemStatus failed");
            return $"Sistem durumu güncellenemedi: {ex.Message}";
        }
    }

    public async Task<(List<ActivityLogDto>? Data, string? Error)> GetActivityLogs(int page = 1, int pageSize = 20)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync($"api/activity/logs?page={page}&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<List<ActivityLogDto>>();
                return (data, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Aktiviteler alınamadı: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActivityLogs failed");
            return (null, $"Aktiviteler alınamadı: {ex.Message}");
        }
    }

    public async Task<(ConfigDto? Data, string? Error)> GetConfig()
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync("api/config");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<ConfigDto>();
                return (data, null);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Yapılandırma alınamadı: {response.StatusCode} - {error}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetConfig failed");
            return (null, $"Yapılandırma alınamadı: {ex.Message}");
        }
    }

    public async Task<(List<UserAdminDto>? Data, string? Error, bool Success)> GetAdminUsersAsync()
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync("api/superadmin/users");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<List<UserAdminDto>>();
                return (data, null, true);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return (null, "Bu sayfayı görüntüleme yetkiniz yok.", false);
            }
            var error = await response.Content.ReadAsStringAsync();
            return (null, $"Kullanıcılar alınamadı: {response.StatusCode} - {error}", false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAdminUsersAsync failed");
            return (null, $"Kullanıcılar alınamadı: {ex.Message}", false);
        }
    }

    public async Task<string?> Disconnect()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/whatsapp/disconnect", null);
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Bağlantı kesilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disconnect failed");
            return $"Bağlantı kesilemedi: {ex.Message}";
        }
    }

    // ===== LOG METHODS =====

    public async Task<(EndpointLogsResponse? Data, string? Error)> GetEndpointLogs(int page = 1, int pageSize = 50, int? statusCode = null)
    {
        try
        {
            var url = $"api/log/endpoints?page={page}&pageSize={pageSize}";
            if (statusCode.HasValue) url += $"&statusCode={statusCode}";
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<EndpointLogsResponse>(), null);
            return (null, $"Loglar alınamadı: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEndpointLogs failed");
            return (null, ex.Message);
        }
    }

    public async Task<(FunctionLogsResponse? Data, string? Error)> GetFunctionLogs(int page = 1, int pageSize = 50)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync($"api/log/functions?page={page}&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<FunctionLogsResponse>(), null);
            return (null, $"Hata logları alınamadı: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFunctionLogs failed");
            return (null, ex.Message);
        }
    }

    public async Task<(LogStatsDto? Data, string? Error)> GetLogStats()
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync("api/log/stats");
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<LogStatsDto>(), null);
            return (null, $"İstatistikler alınamadı: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLogStats failed");
            return (null, ex.Message);
        }
    }

    // ===== PROCESS LOG METHODS =====

    public async Task<(ProcessLogsResponse? Data, string? Error)> GetProcessLogs(int page = 1, int pageSize = 20)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync($"api/log/process?page={page}&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<ProcessLogsResponse>(), null);
            return (null, $"Süreç logları alınamadı: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProcessLogs failed");
            return (null, ex.Message);
        }
    }

    public async Task<(List<ProcessLogDto>? Data, string? Error)> GetProcessTrace(string traceId)
    {
        try
        {
            await EnsureAuthHeader(); var response = await _httpClient.GetAsync($"api/log/process/{traceId}");
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<List<ProcessLogDto>>(), null);
            return (null, $"Trace detayı alınamadı: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetProcessTrace failed");
            return (null, ex.Message);
        }
    }
}

// ===== LOG DTOs (Web katmanı için) =====

public class EndpointLogDto
{
    public int Id { get; set; }
    public string? TraceId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Query { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EndpointLogsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<EndpointLogDto> Logs { get; set; } = new();
}

public class FunctionLogDto
{
    public int Id { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public int LineNumber { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? InputType { get; set; }
    public string? InputValue { get; set; }
    public string Severity { get; set; } = "Error";
    public DateTime CreatedAt { get; set; }
}

public class FunctionLogsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<FunctionLogDto> Logs { get; set; } = new();
}

public class LogStatsDto
{
    public int TotalEndpointLogs { get; set; }
    public int TotalFunctionErrors { get; set; }
    public int Last24hRequests { get; set; }
    public int Last24hErrors { get; set; }
    public int Last24hFunctionErrors { get; set; }
    public int AvgDurationMs { get; set; }
}

// Process Log DTOs
public class ProcessLogDto
{
    public int Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public string Status { get; set; } = "OK";
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProcessTraceSummaryDto
{
    public string TraceId { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalDurationMs { get; set; }
    public bool HasError { get; set; }
    public string LastStepName { get; set; } = string.Empty;
    public string LastStepStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class ProcessLogsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ProcessTraceSummaryDto> Traces { get; set; } = new();
}

