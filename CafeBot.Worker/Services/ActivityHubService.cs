using Microsoft.AspNetCore.SignalR.Client;

namespace CafeBot.Worker.Services;

/// <summary>
/// Worker'dan API'deki ActivityHub'a bildirim göndermek için proxy service
/// </summary>
public class ActivityHubService
{
    private readonly HubConnection _hubConnection;
    private readonly ILogger<ActivityHubService> _logger;

    public ActivityHubService(HubConnection hubConnection, ILogger<ActivityHubService> logger)
    {
        _hubConnection = hubConnection;
        _logger = logger;
    }

    /// <summary>
    /// WhatsApp bağlantısı koptuğunda bildirim gönderir
    /// </summary>
    public async Task NotifyConnectionLostAsync(string userId, string userEmail, string reason)
    {
        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("NotifyConnectionLost", reason);
                _logger.LogInformation("Bağlantı kopma bildirimi gönderildi: {UserId} - {Reason}", userId, reason);
            }
            else
            {
                _logger.LogWarning("SignalR bağlantısı aktif değil, bildirim gönderilemedi: {State}", _hubConnection.State);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı kopma bildirimi gönderilirken hata oluştu");
        }
    }

    /// <summary>
    /// WhatsApp bağlantısı kurulduğunda bildirim gönderir
    /// </summary>
    public async Task NotifyConnectionRestoredAsync(string userId, string userEmail)
    {
        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("NotifyConnectionRestored");
                _logger.LogInformation("Bağlantı kurulma bildirimi gönderildi: {UserId}", userId);
            }
            else
            {
                _logger.LogWarning("SignalR bağlantısı aktif değil, bildirim gönderilemedi: {State}", _hubConnection.State);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı kurulma bildirimi gönderilirken hata oluştu");
        }
    }

    /// <summary>
    /// Genel bağlantı durumu değişikliği bildirimi
    /// </summary>
    public async Task NotifyConnectionStatusAsync(string status, string? details = null)
    {
        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("NotifyConnectionStatus", status, details);
                _logger.LogInformation("Bağlantı durumu bildirimi gönderildi: {Status}", status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı durumu bildirimi gönderilirken hata oluştu");
        }
    }
}