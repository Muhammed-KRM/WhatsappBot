using CafeBot.Business.Interfaces;
using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using CafeBot.Data.Repositories;
using Microsoft.AspNetCore.SignalR.Client;
using CafeBot.Business.DTOs;

namespace CafeBot.Worker.Services;

/// <summary>
/// Gelen WhatsApp mesajlarını işleyen servis.
/// Duplicate kontrolü, shift algılama, parsing, saat seçimi ve yanıt gönderme adımlarını yönetir.
/// </summary>
public class MessageProcessorService
{
    private readonly IMessageParserService _parserService;
    private readonly ISchedulerService _schedulerService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IConfigService _configService;
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly HubConnection _hubConnection;
    private readonly ILogger<MessageProcessorService> _logger;

    public MessageProcessorService(
        IMessageParserService parserService,
        ISchedulerService schedulerService,
        IWhatsAppService whatsAppService,
        IConfigService configService,
        IActivityLogRepository activityLogRepository,
        HubConnection hubConnection,
        ILogger<MessageProcessorService> logger)
    {
        _parserService = parserService;
        _schedulerService = schedulerService;
        _whatsAppService = whatsAppService;
        _configService = configService;
        _activityLogRepository = activityLogRepository;
        _hubConnection = hubConnection;
        _logger = logger;
    }

    /// <summary>
    /// Gelen mesajı işler: duplicate kontrolü, shift algılama, parsing, saat seçimi, yanıt gönderme.
    /// </summary>
    public async Task ProcessMessageAsync(IncomingMessage message)
    {
        try
        {
            // 1. Duplicate check
            if (await _schedulerService.IsMessageProcessedAsync(message.MessageId))
            {
                _logger.LogDebug("Mesaj zaten işlendi, atlanıyor. MessageId: {MessageId}", message.MessageId);
                return;
            }

            // 2. Shift detection
            if (!_parserService.IsShiftMessage(message.Text))
            {
                _logger.LogDebug("Mesaj shift mesajı değil, atlanıyor. MessageId: {MessageId}", message.MessageId);
                return;
            }

            _logger.LogInformation("Shift mesajı algılandı. MessageId: {MessageId}", message.MessageId);
            await LogActivityAsync(ActivityType.Info,
                "Shift mesajı algılandı",
                $"MessageId: {message.MessageId}, GroupId: {message.GroupId}");

            // 3. Parsing
            var slots = await _parserService.ParseShiftMessageAsync(message.Text);
            if (slots == null || slots.Count == 0)
            {
                _logger.LogWarning("Mesaj ayrıştırılamadı veya slot bulunamadı. MessageId: {MessageId}", message.MessageId);
                await LogActivityAsync(ActivityType.Warning,
                    "Mesaj ayrıştırılamadı",
                    $"MessageId: {message.MessageId}");
                return;
            }

            _logger.LogInformation("Mesaj ayrıştırıldı. {SlotCount} slot bulundu. MessageId: {MessageId}",
                slots.Count, message.MessageId);

            // 4. Scheduling - config'den priority list al
            var config = await _configService.GetConfigAsync();
            var selectedHour = await _schedulerService.SelectBestHourAsync(slots, config.PriorityList);

            if (selectedHour == null)
            {
                _logger.LogWarning(
                    "Öncelik listesindeki saatler mevcut değil. MessageId: {MessageId}, PriorityList: [{PriorityList}]",
                    message.MessageId,
                    string.Join(", ", config.PriorityList));
                await LogActivityAsync(ActivityType.Warning,
                    "Uygun saat bulunamadı - öncelik listesindeki saatler mevcut değil",
                    $"MessageId: {message.MessageId}, Mevcut slotlar: {string.Join(", ", slots.Select(s => s.Hour))}");
                return;
            }

            _logger.LogInformation("Saat seçildi: {Hour}. MessageId: {MessageId}", selectedHour, message.MessageId);

            // 5. Sending
            var targetGroupId = config.TargetGroupId;
            if (string.IsNullOrEmpty(targetGroupId))
            {
                _logger.LogError("Hedef grup ID'si yapılandırılmamış. MessageId: {MessageId}", message.MessageId);
                await LogActivityAsync(ActivityType.Error,
                    "Hedef grup yapılandırılmamış",
                    $"MessageId: {message.MessageId}");
                return;
            }

            var sendSuccess = await _whatsAppService.SendMessageAsync(targetGroupId, selectedHour.ToString()!);
            if (!sendSuccess)
            {
                _logger.LogError("Mesaj gönderilemedi. MessageId: {MessageId}, Hour: {Hour}", message.MessageId, selectedHour);
                await LogActivityAsync(ActivityType.Error,
                    $"Mesaj gönderilemedi - saat {selectedHour}",
                    $"MessageId: {message.MessageId}, GroupId: {targetGroupId}");
                return;
            }

            // 6. Mark processed
            await _schedulerService.MarkMessageAsProcessedAsync(message.MessageId, selectedHour.Value);

            // 7. Activity log - başarı
            _logger.LogInformation(
                "Shift mesajı başarıyla işlendi. MessageId: {MessageId}, SelectedHour: {Hour}",
                message.MessageId, selectedHour);
            await LogActivityAsync(ActivityType.Success,
                $"Saat {selectedHour} seçildi ve gönderildi",
                $"MessageId: {message.MessageId}, GroupId: {targetGroupId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj işlenirken hata oluştu. MessageId: {MessageId}", message.MessageId);
            await LogActivityAsync(ActivityType.Error,
                "Mesaj işlenirken beklenmeyen hata",
                $"MessageId: {message.MessageId}, Hata: {ex.Message}");
        }
    }

    private async Task LogActivityAsync(ActivityType type, string message, string? details = null)
    {
        try
        {
            var log = new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Message = message,
                Details = details
            };

            await _activityLogRepository.AddAsync(log);
            await _activityLogRepository.SaveChangesAsync();

            // 8. SignalR - real-time bildirim
            await SendSignalRNotificationAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Aktivite logu kaydedilirken hata oluştu");
        }
    }

    private async Task SendSignalRNotificationAsync(ActivityLog log)
    {
        try
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                var dto = new ActivityLogDto(
                    log.Id,
                    log.Timestamp,
                    log.Type,
                    log.Message,
                    log.Details);

                await _hubConnection.InvokeAsync("SendActivityUpdate", dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR bildirimi gönderilemedi");
        }
    }
}
