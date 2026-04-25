using CafeBot.Business.Infrastructure.WhatsApp;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using CafeBot.Data.Repositories;
using CafeBot.Data.Entities;
using CafeBot.Data.Context;
using CafeBot.Data.Interfaces;
using CafeBot.Business.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.Worker.Services;

/// <summary>
/// Evolution API'yi polling yöntemiyle dinleyen ve yeni mesajları MessageProcessorService'e ileten BackgroundService.
/// </summary>
public class WhatsAppListenerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppListenerService> _logger;

    // Son işlenen mesajın timestamp'i (duplicate önleme)
    // Başlangıçta son 5 dakikadaki mesajları da kontrol etmesi için 5 dakika geri çekiyoruz.
    private DateTime _lastProcessedAt = DateTime.UtcNow.AddMinutes(-5);

    // Daha önce görülen mesaj ID'leri (in-memory duplicate guard)
    private readonly HashSet<string> _seenMessageIds = new();

    private const int PollingIntervalSeconds = 5;

    public WhatsAppListenerService(
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppListenerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WhatsAppListenerService başlatıldı. (Sadece durum izleme modu — mesaj işleme webhook tarafından yapılıyor)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var users = await dbContext.Users.Select(u => new { u.Id, u.Email }).ToListAsync(stoppingToken);

                foreach (var user in users)
                {
                    try
                    {
                        var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();
                        var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                        
                        currentUserService.SetCurrentUserId(user.Id);

                        var config = await configService.GetConfigAsync();
                        
                        if (config.SystemStatus != SystemStatus.Running) continue;

                        var oldStatus = config.ConnectionStatus;
                        var currentStatus = await whatsAppService.GetConnectionStatusAsync();

                        if (oldStatus == ConnectionStatus.Connected && currentStatus == ConnectionStatus.Disconnected)
                        {
                            _logger.LogWarning("DİKKAT: Kullanıcı {Email} ({UserId}) için WhatsApp bağlantısı düştü! Bildirim tetikleniyor.", user.Email, user.Id);
                            await notificationService.SendConnectionLostAlertAsync(config.SessionId ?? $"cafebot_{user.Id}", DateTime.UtcNow, user.Email!);
                        }

                        if (currentStatus == ConnectionStatus.Connected)
                        {
                            var groupNamesStr = config.TargetGroupNames != null && config.TargetGroupNames.Count > 0 ? string.Join(", ", config.TargetGroupNames) : "Grup Yok";
                            var groupIdsStr = config.TargetGroupIds != null && config.TargetGroupIds.Count > 0 ? string.Join(", ", config.TargetGroupIds) : "Grup Yok";
                            await LogActivityAsync(scope, ActivityType.Info, "WhatsApp bağlantısı aktif, webhook üzerinden dinleniyor...", $"Grup: {groupNamesStr} ({groupIdsStr})");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Kullanıcı {Email} ({UserId}) için durum kontrolü sırasında hata oluştu", user.Email, user.Id);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsAppListenerService döngüsünde hata oluştu");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("WhatsAppListenerService durduruldu.");
    }

    private async Task ListenForMessagesAsync(
        string? sessionId,
        string? targetGroupId,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        // ... (Bu method kullanılmıyor, webhook üzerinden işleniyor ancak geriye dönük uyumluluk için duruyor)
        await Task.CompletedTask;
    }

    private async Task LogActivityAsync(Microsoft.Extensions.DependencyInjection.AsyncServiceScope scope, ActivityType type, string message, string? details = null)
    {
        try
        {
            var repo = scope.ServiceProvider.GetRequiredService<IActivityLogRepository>();
            var hub = scope.ServiceProvider.GetRequiredService<HubConnection>();

            var log = new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Message = message,
                Details = details
            };

            await repo.AddAsync(log);
            await repo.SaveChangesAsync();

            if (hub.State == HubConnectionState.Connected)
            {
                await hub.InvokeAsync("SendActivityUpdate", new ActivityLogDto(log.Id, log.Timestamp, log.Type, log.Message, log.Details));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aktivite günlüğü yazılırken hata oluştu");
        }
    }
}
