using CafeBot.Business.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CafeBot.API.Hubs;

/// <summary>
/// Gerçek zamanlı aktivite güncellemelerini Blazor Web UI'a ileten SignalR Hub'ı.
/// Worker servisi IHubContext&lt;ActivityHub&gt; üzerinden bu hub'a mesaj gönderebilir.
/// </summary>
public class ActivityHub : Hub
{
    private readonly ILogger<ActivityHub> _logger;

    public ActivityHub(ILogger<ActivityHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Bağlı tüm istemcilere aktivite güncellemesi gönderir.
    /// </summary>
    public async Task SendActivityUpdate(ActivityLogDto activity)
    {
        _logger.LogDebug("Aktivite güncellemesi gönderiliyor: {ActivityType} - {Message}", activity.Type, activity.Message);
        await Clients.All.SendAsync("ReceiveActivity", activity);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR istemcisi bağlandı: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR istemcisi bağlantısı kesildi: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
