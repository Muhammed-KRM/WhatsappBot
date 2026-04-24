using System.Net.Http.Json;
using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;

namespace CafeBot.Web.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
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
            var response = await _httpClient.GetAsync("api/whatsapp/status");
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

    public async Task<(List<GroupDto>? Data, string? Error)> GetGroups()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/whatsapp/groups");
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

    public async Task<string?> UpdateGroup(string groupId, string groupName)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/config/group", new { GroupId = groupId, GroupName = groupName });
            if (response.IsSuccessStatusCode)
                return null;
            var error = await response.Content.ReadAsStringAsync();
            return $"Grup kaydedilemedi: {response.StatusCode} - {error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGroup failed");
            return $"Grup kaydedilemedi: {ex.Message}";
        }
    }

    public async Task<string?> UpdatePriority(List<int> priorityList)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/config/priority", priorityList);
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

    public async Task<string?> UpdateSystemStatus(SystemStatus status)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/config/system-status", status);
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
            var response = await _httpClient.GetAsync($"api/activity/logs?page={page}&pageSize={pageSize}");
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
            var response = await _httpClient.GetAsync("api/config");
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
            var response = await _httpClient.GetAsync(url);
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
            var response = await _httpClient.GetAsync($"api/log/functions?page={page}&pageSize={pageSize}");
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
            var response = await _httpClient.GetAsync("api/log/stats");
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
            var response = await _httpClient.GetAsync($"api/log/process?page={page}&pageSize={pageSize}");
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
            var response = await _httpClient.GetAsync($"api/log/process/{traceId}");
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

