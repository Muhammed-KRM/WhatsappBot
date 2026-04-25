using System.Diagnostics;
using System.Text.Json;
using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using CafeBot.Data.Repositories;
using CafeBot.Data.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CafeBot.API.Controllers;

/// <summary>
/// Evolution API'den gelen webhook event'lerini işleyen controller.
/// MESSAGES_UPSERT event'i ile yeni mesajları alır ve işler.
/// Her adım ProcessLog tablosuna kaydedilir (breakpoint mantığı).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookController> _logger;

    // Paralel işlemeyi önlemek için (Evolution API'yi korumak adına)
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    // In-memory duplicate guard
    private static readonly HashSet<string> _processedIds = new();
    private static readonly object _lock = new();

    // Noisy Neighbor Koruma (Rate Limiting)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime WindowStart, int Count)> _rateLimits = new();
    private const int MaxRequestsPerSecondPerTenant = 5;

    private readonly IConfiguration _configuration;

    public WebhookController(IServiceScopeFactory scopeFactory, ILogger<WebhookController> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Evolution API webhook endpoint.
    /// Mesaj geldiğinde Evolution API bu endpoint'e POST yapar.
    /// </summary>
    [HttpPost("whatsapp")]
    public IActionResult ReceiveWhatsAppEvent([FromQuery] string? token, [FromBody] JsonElement payload)
    {
        // Güvenlik: Webhook Token Kontrolü
        var expectedToken = _configuration["Webhook:SecurityToken"];
        if (!string.IsNullOrEmpty(expectedToken) && token != expectedToken)
        {
            _logger.LogWarning("Webhook yetkisiz erişim denemesi reddedildi. Token uyumsuz veya eksik.");
            return Unauthorized(new { error = "Invalid or missing token" });
        }

        // ── Gürültülü Komşu (Noisy Neighbor) Koruması ──
        string instanceName = payload.TryGetProperty("instance", out var instEl) ? instEl.GetString() ?? "default" : "default";
        var now = DateTime.UtcNow;

        _rateLimits.AddOrUpdate(instanceName, 
            _ => (now, 1), 
            (_, current) => 
            {
                // 1 saniyelik pencere dolduysa sıfırla
                if ((now - current.WindowStart).TotalSeconds > 1)
                    return (now, 1);
                
                return (current.WindowStart, current.Count + 1);
            });

        if (_rateLimits.TryGetValue(instanceName, out var rateInfo) && rateInfo.Count > MaxRequestsPerSecondPerTenant)
        {
            _logger.LogWarning("Rate limit aşıldı! Instance: {InstanceName}. Mesaj yoksayılıyor.", instanceName);
            // 429 döndürsek Evolution API retry yapabilir, bu da kuyruğu tıkar. O yüzden 200 dönüp yoksayıyoruz.
            return Ok(new { status = "rate_limited" });
        }

        // Webhook'u bekletmeden 200 dön (Evolution API zaman aşımına uğramasın)
        // Ancak arka planda sırayla işle
        _ = Task.Run(async () =>
        {
            await _semaphore.WaitAsync();
            try
            {
                await ProcessWebhookAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook işlenirken hata oluştu");
            }
            finally
            {
                _semaphore.Release();
            }
        });

        return Ok(new { status = "received" });
    }

    private async Task ProcessWebhookAsync(JsonElement payload)
    {
        try
        {
            // Event türünü al
            string? eventType = null;
            if (payload.TryGetProperty("event", out var eventEl))
                eventType = eventEl.GetString();

            _logger.LogInformation("Webhook alındı. Event: {Event}", eventType ?? "unknown");

            // Sadece mesaj event'lerini işle
            if (eventType != "messages.upsert" && eventType != "MESSAGES_UPSERT")
            {
                _logger.LogDebug("Mesaj event'i değil, atlanıyor. Event: {Event}", eventType);
                return;
            }

            // data objesini al
            if (!payload.TryGetProperty("data", out var dataEl))
            {
                _logger.LogWarning("Webhook payload'ında 'data' bulunamadı.");
                return;
            }

            // data tek obje veya array olabilir
            var messages = new List<JsonElement>();
            if (dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataEl.EnumerateArray())
                    messages.Add(item);
            }
            else if (dataEl.ValueKind == JsonValueKind.Object)
            {
                messages.Add(dataEl);
            }

            foreach (var msgData in messages)
            {
                try
                {
                    await ProcessSingleMessageAsync(msgData, payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tek mesaj işlenirken hata");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook işlenirken beklenmeyen hata");
        }
    }

    private async Task ProcessSingleMessageAsync(JsonElement msgData, JsonElement payload)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();
        var parserService = scope.ServiceProvider.GetRequiredService<IMessageParserService>();
        var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();
        var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityLogRepository>();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var processStart = Stopwatch.StartNew();
        var stepOrder = 0;
        string traceId = "unknown";

        // Helper: ProcessLog kaydet
        async Task LogStep(string stepName, string functionName, string? input, string? output, string status, string? error = null, int durationMs = 0)
        {
            try
            {
                stepOrder++;
                db.ProcessLogs.Add(new ProcessLog
                {
                    TraceId = traceId,
                    StepOrder = stepOrder,
                    StepName = stepName,
                    FunctionName = functionName,
                    InputData = input?.Length > 2000 ? input[..2000] + "...[truncated]" : input,
                    OutputData = output?.Length > 2000 ? output[..2000] + "...[truncated]" : output,
                    Status = status,
                    ErrorMessage = error?.Length > 1000 ? error[..1000] + "..." : error,
                    DurationMs = durationMs,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessLog kaydedilemedi: {StepName}", stepName);
            }
        }

        try
        {
            // ── STEP 1: Webhook Received ──
            var stepSw = Stopwatch.StartNew();
            string? eventType = payload.TryGetProperty("event", out var evEl) ? evEl.GetString() : "unknown";
            
            // ── Robust Instance Extraction ──
            string? instanceFromPayload = null;
            if (payload.TryGetProperty("instance", out var instEl))
                instanceFromPayload = instEl.GetString();
            else if (payload.TryGetProperty("instanceName", out var instanceNameEl))
                instanceFromPayload = instanceNameEl.GetString();
            else if (payload.TryGetProperty("data", out var dataEl2) && 
                     dataEl2.TryGetProperty("instance", out var dataInstanceEl))
                instanceFromPayload = dataInstanceEl.GetString();
            
            // Eğer instance adı cafebot_XXX formatındaysa, tenant kimliğini al ve ata
            if (!string.IsNullOrEmpty(instanceFromPayload) && instanceFromPayload.StartsWith("cafebot_"))
            {
                var tenantId = instanceFromPayload.Substring("cafebot_".Length);
                currentUserService.SetCurrentUserId(tenantId);
            }

            await LogStep("WebhookReceived", "WebhookController.ProcessSingleMessageAsync",
                $"{{\"event\":\"{eventType}\",\"instance\":\"{instanceFromPayload}\"}}",
                null, "OK", null, (int)stepSw.ElapsedMilliseconds);

            // ── STEP 2: Config Loaded ──
            stepSw.Restart();
            var config = await configService.GetConfigAsync();
            
            await LogStep("ConfigLoaded", "ConfigService.GetConfigAsync",
                null,
                $"{{\"SystemStatus\":\"{config.SystemStatus}\",\"ConnectionStatus\":\"{config.ConnectionStatus}\",\"TargetGroupIds\":[{string.Join(",", config.TargetGroupIds.Select(id => $"\"{id}\""))}],\"SessionId\":\"{config.SessionId}\",\"PriorityList\":[{string.Join(",", config.PriorityList)}]}}",
                "OK", null, (int)stepSw.ElapsedMilliseconds);

            // Sistem çalışmıyor mu?
            if (config.SystemStatus != SystemStatus.Running)
            {
                await LogStep("SystemCheck", "WebhookController",
                    $"{{\"SystemStatus\":\"{config.SystemStatus}\"}}",
                    null, "Skip", $"Sistem Running değil: {config.SystemStatus}");
                return;
            }

            // Hedef grup seçilmemiş mi?
            var targetGroupIds = config.TargetGroupIds;
            if (targetGroupIds == null || targetGroupIds.Count == 0)
            {
                await LogStep("GroupCheck", "WebhookController",
                    null, null, "Skip", "Hedef grup seçilmemiş");
                return;
            }

            // WhatsApp bağlı değil mi?
            if (config.ConnectionStatus != ConnectionStatus.Connected)
            {
                await LogStep("ConnectionCheck", "WebhookController",
                    $"{{\"ConnectionStatus\":\"{config.ConnectionStatus}\"}}",
                    null, "Skip", $"WhatsApp bağlı değil: {config.ConnectionStatus}");
                await LogActivity(activityRepo, ActivityType.Warning, 
                    "WhatsApp bağlı değil - mesaj işlenemedi", 
                    $"Connection Status: {config.ConnectionStatus}. QR kod ile yeniden bağlanın.");
                return;
            }

            // ── STEP 3: Message Validated ──
            stepSw.Restart();
            
            // key objesini al
            if (!msgData.TryGetProperty("key", out var keyEl))
            {
                await LogStep("MessageValidated", "WebhookController",
                    null, null, "Error", "Mesaj data'sında 'key' bulunamadı");
                return;
            }

            var remoteJid = keyEl.TryGetProperty("remoteJid", out var jidEl) ? jidEl.GetString() : null;
            var fromMe = keyEl.TryGetProperty("fromMe", out var fromMeEl) && fromMeEl.GetBoolean();
            var messageId = keyEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            
            // TraceId'yi messageId yap
            traceId = messageId ?? $"no-id-{DateTime.UtcNow.Ticks}";

            // RemoteJid kontrol
            if (string.IsNullOrEmpty(remoteJid) || !targetGroupIds.Contains(remoteJid))
            {
                await LogStep("MessageValidated", "WebhookController",
                    $"{{\"remoteJid\":\"{remoteJid}\",\"targetGroupIds\":[{string.Join(",", targetGroupIds.Select(id => $"\"{id}\""))}],\"fromMe\":{fromMe.ToString().ToLower()},\"messageId\":\"{messageId}\"}}",
                    null, "Skip", $"RemoteJid uyuşmuyor. Gelen: {remoteJid}",
                    (int)stepSw.ElapsedMilliseconds);
                return;
            }

            // fromMe kontrolü
            if (fromMe)
            {
                await LogStep("MessageValidated", "WebhookController",
                    $"{{\"remoteJid\":\"{remoteJid}\",\"fromMe\":true,\"messageId\":\"{messageId}\"}}",
                    null, "Skip", "Kendi mesajımız, atlanıyor",
                    (int)stepSw.ElapsedMilliseconds);
                return;
            }

            if (string.IsNullOrEmpty(messageId))
            {
                await LogStep("MessageValidated", "WebhookController",
                    null, null, "Error", "MessageId bulunamadı");
                return;
            }

            await LogStep("MessageValidated", "WebhookController",
                $"{{\"remoteJid\":\"{remoteJid}\",\"fromMe\":{fromMe.ToString().ToLower()},\"messageId\":\"{messageId}\"}}",
                $"{{\"valid\":true}}", "OK", null, (int)stepSw.ElapsedMilliseconds);

            // ── STEP 3.5: Deduplication Check ──
            stepSw.Restart();
            var isProcessed = await schedulerService.IsMessageProcessedAsync(messageId);
            if (isProcessed)
            {
                await LogStep("DeduplicationCheck", "SchedulerService.IsMessageProcessedAsync",
                    $"{{\"messageId\":\"{messageId}\"}}",
                    $"{{\"isProcessed\":true}}", "Skip", "Mesaj daha önce işlenmiş (Çift Yanıt Koruması)", 
                    (int)stepSw.ElapsedMilliseconds);
                
                _logger.LogInformation("Mesaj {MessageId} daha önce işlendiği için atlanıyor (Deduplication).", messageId);
                return;
            }
            
            await LogStep("DeduplicationCheck", "SchedulerService.IsMessageProcessedAsync",
                    $"{{\"messageId\":\"{messageId}\"}}",
                    $"{{\"isProcessed\":false}}", "OK", "Mesaj yeni", 
                    (int)stepSw.ElapsedMilliseconds);

            // ── STEP 4: Instance Resolved ──
            stepSw.Restart();
            string? webhookInstanceName = instanceFromPayload;

            if (string.IsNullOrEmpty(webhookInstanceName))
            {
                // Fallback: Mevcut aktif session'ları listele ve ilkini kullan
                try
                {
                    var instances = await whatsAppService.GetAllInstancesAsync();
                    if (instances.Count > 0)
                    {
                        webhookInstanceName = instances.First();
                        await LogStep("InstanceResolved", "WhatsAppService.GetAllInstancesAsync",
                            "{\"source\":\"fallback\"}",
                            $"{{\"instanceName\":\"{webhookInstanceName}\",\"totalInstances\":{instances.Count}}}",
                            "OK", "Webhook'ta instance bulunamadı, fallback kullanıldı",
                            (int)stepSw.ElapsedMilliseconds);
                    }
                    else
                    {
                        await LogStep("InstanceResolved", "WebhookController",
                            null, null, "Error", "Hiç aktif instance bulunamadı");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    await LogStep("InstanceResolved", "WhatsAppService.GetAllInstancesAsync",
                        null, null, "Error", $"Instance listesi alınamadı: {ex.Message}");
                    return;
                }
            }
            else
            {
                // SessionId eşleşme kontrolü
                string sessionMatch = config.SessionId == webhookInstanceName ? "match" : "mismatch";
                await LogStep("InstanceResolved", "WebhookController",
                    $"{{\"webhookInstance\":\"{webhookInstanceName}\",\"dbSessionId\":\"{config.SessionId}\"}}",
                    $"{{\"resolved\":\"{webhookInstanceName}\",\"sessionMatch\":\"{sessionMatch}\"}}",
                    "OK", null, (int)stepSw.ElapsedMilliseconds);
            }

            // SessionId uyuşmazlığı varsa güncelle
            if (!string.IsNullOrEmpty(config.SessionId) && config.SessionId != webhookInstanceName)
            {
                await configService.UpdateSessionIdAsync(webhookInstanceName);
                config = await configService.GetConfigAsync();
            }

            // ── Duplicate Check ──
            lock (_lock)
            {
                if (_processedIds.Contains(messageId))
                    return;
                _processedIds.Add(messageId);
                if (_processedIds.Count > 1000)
                    _processedIds.Clear();
            }

            if (await schedulerService.IsMessageProcessedAsync(messageId))
            {
                await LogStep("DuplicateCheck", "SchedulerService.IsMessageProcessedAsync",
                    $"{{\"messageId\":\"{messageId}\"}}",
                    null, "Skip", "Mesaj zaten işlenmiş (duplicate)");
                return;
            }

            // ── STEP 5: Text Extracted ──
            stepSw.Restart();
            string? text = null;
            string? textSource = null;
            if (msgData.TryGetProperty("message", out var messageEl))
            {
                try
                {
                    if (messageEl.TryGetProperty("conversation", out var convEl))
                    {
                        text = convEl.GetString();
                        textSource = "conversation";
                    }
                    else if (messageEl.TryGetProperty("extendedTextMessage", out var extEl) &&
                             extEl.TryGetProperty("text", out var extTextEl))
                    {
                        text = extTextEl.GetString();
                        textSource = "extendedTextMessage";
                    }
                }
                catch (InvalidOperationException)
                {
                    // UTF-8 transcode hatası — raw text'i al
                    _logger.LogWarning("Mesaj metni UTF-8 decode hatası verdi, raw text kullanılıyor. MessageId: {Id}", messageId);
                    textSource = "rawText-fallback";
                    try
                    {
                        if (messageEl.TryGetProperty("conversation", out var convElRaw))
                            text = convElRaw.GetRawText().Trim('"');
                        else if (messageEl.TryGetProperty("extendedTextMessage", out var extElRaw) &&
                                 extElRaw.TryGetProperty("text", out var extTextElRaw))
                            text = extTextElRaw.GetRawText().Trim('"');
                    }
                    catch (Exception rawEx)
                    {
                        await LogStep("TextExtracted", "WebhookController",
                            $"{{\"source\":\"{textSource}\"}}", null, "Error",
                            $"Raw text de okunamadı: {rawEx.Message}");
                        return;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                // Mesaj property'lerini logla — neden text bulunamadı?
                string msgKeys = "[]";
                try
                {
                    if (msgData.TryGetProperty("message", out var msgEl2))
                        msgKeys = $"[{string.Join(", ", msgEl2.EnumerateObject().Select(p => p.Name))}]";
                }
                catch { }
                
                await LogStep("TextExtracted", "WebhookController",
                    $"{{\"messageKeys\":{msgKeys},\"messageType\":\"{(msgData.TryGetProperty("messageType", out var mtEl) ? mtEl.GetString() : "unknown")}\"}}",
                    null, "Skip", "Mesaj metni boş veya bulunamadı",
                    (int)stepSw.ElapsedMilliseconds);
                return;
            }

            string textPreview = text.Length > 200 ? text[..200] + "..." : text;
            await LogStep("TextExtracted", "WebhookController",
                $"{{\"source\":\"{textSource}\"}}",
                $"{{\"text\":\"{EscapeJson(textPreview)}\",\"length\":{text.Length}}}",
                "OK", null, (int)stepSw.ElapsedMilliseconds);

            // Aktivite logu
            await LogActivity(activityRepo, ActivityType.Info,
                "Webhook mesajı alındı",
                $"MessageId: {messageId}, Text: {(text.Length > 50 ? text[..50] + "..." : text)}");

            // ── STEP 6: Shift Detected ──
            stepSw.Restart();
            var isShift = parserService.IsShiftMessage(text);
            
            await LogStep("ShiftDetected", "MessageParserService.IsShiftMessage",
                $"{{\"text\":\"{EscapeJson(textPreview)}\"}}",
                $"{{\"isShift\":{isShift.ToString().ToLower()}}}",
                isShift ? "OK" : "Skip",
                isShift ? null : "Shift mesajı değil",
                (int)stepSw.ElapsedMilliseconds);

            if (!isShift)
                return;

            await LogActivity(activityRepo, ActivityType.Info, "Shift mesajı algılandı", $"MessageId: {messageId}");

            // ── STEP 7: Slots Parsed ──
            stepSw.Restart();
            var slots = await parserService.ParseShiftMessageAsync(text, remoteJid);
            
            if (slots == null || slots.Count == 0)
            {
                await LogStep("SlotsParsed", "MessageParserService.ParseShiftMessageAsync",
                    $"{{\"text\":\"{EscapeJson(textPreview)}\"}}",
                    "{\"slots\":[]}", "Error", "Mesaj ayrıştırılamadı - slot bulunamadı",
                    (int)stepSw.ElapsedMilliseconds);
                await LogActivity(activityRepo, ActivityType.Warning, "Mesaj ayrıştırılamadı", $"MessageId: {messageId}, Text: {text}");
                return;
            }

            string slotsJson = $"[{string.Join(",", slots.Select(s => $"{{\"hour\":{s.Hour},\"count\":{s.PersonCount}}}"))}]";
            await LogStep("SlotsParsed", "MessageParserService.ParseShiftMessageAsync",
                $"{{\"text\":\"{EscapeJson(textPreview)}\"}}",
                $"{{\"slots\":{slotsJson},\"slotCount\":{slots.Count}}}",
                "OK", null, (int)stepSw.ElapsedMilliseconds);

            // ── STEP 8: Hour Selected ──
            stepSw.Restart();
            var groupPriorityList = config.PriorityList;
            if (!string.IsNullOrEmpty(remoteJid))
            {
                var groupSettings = await configService.GetGroupSettingsAsync(remoteJid);
                if (groupSettings != null && groupSettings.PriorityList != null && groupSettings.PriorityList.Count > 0)
                {
                    groupPriorityList = groupSettings.PriorityList;
                }
            }

            var selectedHour = await schedulerService.SelectBestHourAsync(slots, groupPriorityList);
            
            if (selectedHour == null)
            {
                await LogStep("HourSelected", "SchedulerService.SelectBestHourAsync",
                    $"{{\"slots\":{slotsJson},\"priorityList\":[{string.Join(",", groupPriorityList)}]}}",
                    null, "Error", "Uygun saat bulunamadı",
                    (int)stepSw.ElapsedMilliseconds);
                await LogActivity(activityRepo, ActivityType.Warning,
                    "Uygun saat bulunamadı",
                    $"Slots: {string.Join(", ", slots.Select(s => $"{s.Hour}:00"))}, Priority: [{string.Join(",", groupPriorityList)}]");
                return;
            }

            await LogStep("HourSelected", "SchedulerService.SelectBestHourAsync",
                $"{{\"slots\":{slotsJson},\"priorityList\":[{string.Join(",", groupPriorityList)}]}}",
                $"{{\"selectedHour\":{selectedHour}}}",
                "OK", null, (int)stepSw.ElapsedMilliseconds);

            // ── STEP 9: Send Attempt ──
            stepSw.Restart();
            await LogStep("SendAttempt", "WhatsAppService.SendMessageAsync",
                $"{{\"instanceName\":\"{webhookInstanceName}\",\"targetGroupId\":\"{remoteJid}\",\"message\":\"{selectedHour}\",\"dbSessionId\":\"{config.SessionId}\"}}",
                null, "OK", null, 0);

            var sendSuccess = await whatsAppService.SendMessageAsync(webhookInstanceName, remoteJid, selectedHour.ToString()!, traceId);

            // ── STEP 11: Process Completed ──
            if (sendSuccess)
            {
                // İşlendi olarak işaretle
                await schedulerService.MarkMessageAsProcessedAsync(messageId, selectedHour.Value);

                await LogStep("ProcessCompleted", "WebhookController",
                    null,
                    $"{{\"result\":\"SUCCESS\",\"selectedHour\":{selectedHour},\"totalDurationMs\":{processStart.ElapsedMilliseconds}}}",
                    "OK", null, (int)processStart.ElapsedMilliseconds);

                _logger.LogInformation("✅ Saat {Hour} seçildi ve gönderildi! MessageId: {Id}", selectedHour, messageId);
                await LogActivity(activityRepo, ActivityType.Success,
                    $"Saat {selectedHour} seçildi ve gönderildi ✅",
                    $"MessageId: {messageId}, GroupId: {remoteJid}");
            }
            else
            {
                await LogStep("ProcessCompleted", "WebhookController",
                    null,
                    $"{{\"result\":\"FAILED\",\"totalDurationMs\":{processStart.ElapsedMilliseconds}}}",
                    "Error", "Mesaj gönderilemedi",
                    (int)processStart.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            await LogStep("UnexpectedError", "WebhookController.ProcessSingleMessageAsync",
                null, null, "Error",
                $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}",
                (int)processStart.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>JSON string'i escape eder (çift tırnak ve backslash)</summary>
    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

    private static async Task LogActivity(IActivityLogRepository repo, ActivityType type, string message, string? details = null)
    {
        var log = new ActivityLog
        {
            Timestamp = DateTime.UtcNow,
            Type = type,
            Message = message,
            Details = details
        };
        await repo.AddAsync(log);
        await repo.SaveChangesAsync();
    }
}
