using CafeBot.Business.Infrastructure.WhatsApp;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using CafeBot.Data.Repositories;
using CafeBot.Data.Entities;
using CafeBot.Business.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

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

        // NOT: Mesaj polling DEVRE DIŞI bırakıldı.
        // Sebep: Worker'ın her 5 saniyede findMessages çağırması WhatsApp rate-overlimit hatasına neden oluyordu.
        // Mesajlar artık sadece Evolution API webhook → WebhookController üzerinden işleniyor.
        // Bu servis sadece periyodik bağlantı durumu izleme ve log tutma görevi yapar.

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();

                var config = await configService.GetConfigAsync();

                if (config.SystemStatus == SystemStatus.Running &&
                    config.ConnectionStatus == ConnectionStatus.Connected)
                {
                    // Her 1 dakikada bir "dinliyorum" logu at (durum takibi için)
                    await LogActivityAsync(ActivityType.Info, "WhatsApp dinleniyor...", $"Grup: {config.TargetGroupName} ({config.TargetGroupId})");
                }
                else
                {
                    _logger.LogDebug(
                        "Sistem aktif değil veya bağlantı yok. SystemStatus: {SystemStatus}, ConnectionStatus: {ConnectionStatus}",
                        config.SystemStatus, config.ConnectionStatus);
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
                // 60 saniye aralıkla durum kontrolü (eskiden 5 sn'de bir polling yapılıyordu)
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
        if (string.IsNullOrEmpty(sessionId))
        {
            _logger.LogDebug("Session ID yapılandırılmamış, mesaj dinleme atlanıyor.");
            return;
        }

        if (string.IsNullOrEmpty(targetGroupId))
        {
            _logger.LogDebug("Hedef grup ID'si yapılandırılmamış, mesaj dinleme atlanıyor.");
            return;
        }

        try
        {
            var evolutionClient = serviceProvider.GetRequiredService<EvolutionApiClient>();
            var rawMessages = await evolutionClient.GetMessagesAsync(sessionId, targetGroupId);

            if (rawMessages == null || rawMessages.Count == 0)
                return;

            // EvolutionMessage → IncomingMessage dönüşümü ve filtreleme
            var newMessages = new List<IncomingMessage>();
            foreach (var msg in rawMessages)
            {
                // Sadece karşı taraftan gelen mesajlar (VEYA kendimizden gelenler, test için izin veriyoruz)
                // Ancak botun kendi cevabına (örn: "19") tekrar cevap vermesini önlemek için IsShiftMessage kontrolüne güveniyoruz.
                // if (msg.MessageKey?.FromMe == true)
                //    continue;

                var messageId = msg.MessageKey?.Id ?? msg.Id;
                if (string.IsNullOrEmpty(messageId))
                    continue;

                // Timestamp dönüşümü (Unix epoch)
                var receivedAt = msg.MessageTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(msg.MessageTimestamp.Value).UtcDateTime
                    : DateTime.UtcNow;

                // Sadece son işlemeden sonraki mesajlar
                if (receivedAt <= _lastProcessedAt)
                    continue;

                // Mesaj metnini al
                var text = msg.Message?.Conversation
                    ?? msg.Message?.ExtendedTextMessage?.Text
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                newMessages.Add(new IncomingMessage
                {
                    MessageId = messageId,
                    GroupId = msg.MessageKey?.RemoteJid ?? targetGroupId,
                    Text = text,
                    ReceivedAt = receivedAt
                });
            }

            if (newMessages.Count == 0)
                return;

            _logger.LogDebug("{Count} yeni mesaj alındı.", newMessages.Count);

            var processor = serviceProvider.GetRequiredService<MessageProcessorService>();

            foreach (var msg in newMessages.OrderBy(m => m.ReceivedAt))
            {
                if (ct.IsCancellationRequested)
                    break;

                // In-memory duplicate guard
                if (_seenMessageIds.Contains(msg.MessageId))
                    continue;

                _seenMessageIds.Add(msg.MessageId);

                // Bellek sızıntısını önlemek için eski ID'leri temizle
                if (_seenMessageIds.Count > 1000)
                    _seenMessageIds.Clear();

                try
                {
                    await processor.ProcessMessageAsync(msg);

                    // Son işlenen zamanı güncelle
                    if (msg.ReceivedAt > _lastProcessedAt)
                        _lastProcessedAt = msg.ReceivedAt;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Mesaj işlenirken hata. MessageId: {MessageId}", msg.MessageId);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Evolution API'ye bağlanılamadı. Bağlantı durumu güncelleniyor.");

            try
            {
                var configService = serviceProvider.GetRequiredService<IConfigService>();
                await configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Bağlantı durumu güncellenirken hata oluştu");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj dinleme sırasında beklenmeyen hata oluştu");
        }
    }

    private async Task LogActivityAsync(ActivityType type, string message, string? details = null)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
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
        catch { /* ignored */ }
    }
}
