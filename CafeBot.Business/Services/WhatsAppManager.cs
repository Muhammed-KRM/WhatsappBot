using CafeBot.Business.DTOs;
using CafeBot.Business.Infrastructure.WhatsApp;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using CafeBot.Data.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace CafeBot.Business.Services;

public class WhatsAppManager : IWhatsAppService
{
    private readonly EvolutionApiClient _evolutionApiClient;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;
    private readonly ILogger<WhatsAppManager> _logger;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly ICurrentUserService _currentUserService;

    private string SessionName => $"cafebot_{_currentUserService.UserId}";
    private string GroupsCacheKey => $"cafebot:groups:{_currentUserService.UserId}";

    public WhatsAppManager(
        EvolutionApiClient evolutionApiClient,
        IConfigService configService,
        ILogService logService,
        ILogger<WhatsAppManager> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        ICurrentUserService currentUserService)
    {
        _evolutionApiClient = evolutionApiClient ?? throw new ArgumentNullException(nameof(evolutionApiClient));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<QRCodeDto> InitializeSessionAsync(string? externalTraceId = null)
    {
        var traceId = externalTraceId ?? Guid.NewGuid().ToString("N")[..8];
        var step = 10; // Controller adımlarından sonra devam et
        try
        {
            _logger.LogInformation("🚀 [TRACE:{TraceId}] ========== InitializeSessionAsync BAŞLADI ==========", traceId);
            
            // Step: Giriş bilgileri
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "InitializeSession Giriş", "WhatsAppManager.InitializeSessionAsync",
                inputData: $"{{\"sessionName\":\"{SessionName}\",\"userId\":\"{_currentUserService.UserId}\"}}",
                outputData: null, status: "OK");

            // Step: ConnectionStatus → Connecting
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Status → Connecting", "ConfigService.UpdateConnectionStatusAsync",
                inputData: "{\"newStatus\":\"Connecting\"}", outputData: null, status: "OK");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Connecting);

            // Step: Eski session silme
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Eski Session Silme", "EvolutionApiClient.DeleteSessionAsync",
                inputData: $"{{\"sessionName\":\"{SessionName}\"}}", outputData: null, status: "BAŞLADI");
            await _evolutionApiClient.DeleteSessionAsync(SessionName);
            await Task.Delay(1000);
            await _logService.LogProcessStepAsync(traceId, step, "Eski Session Silme", "EvolutionApiClient.DeleteSessionAsync",
                inputData: null, outputData: "Silindi + 1sn beklendi", status: "OK");

            // Step: Yeni session oluştur
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Yeni Session Oluştur", "EvolutionApiClient.CreateSessionAsync",
                inputData: $"{{\"sessionName\":\"{SessionName}\",\"qrcode\":true}}", outputData: null, status: "BAŞLADI");
            var createResponse = await _evolutionApiClient.CreateSessionAsync(SessionName);
            var realInstanceName = createResponse.InstanceData?.InstanceName ?? SessionName;
            await _logService.LogProcessStepAsync(traceId, step, "Yeni Session Oluştur", "EvolutionApiClient.CreateSessionAsync",
                inputData: null,
                outputData: $"{{\"realInstanceName\":\"{realInstanceName}\",\"statusInfo\":\"{createResponse.StatusInfo}\",\"hasQrCode\":{(createResponse.QrCodeData != null).ToString().ToLower()},\"instanceId\":\"{createResponse.InstanceData?.InstanceId}\"}}",
                status: "OK");

            // Step: Instance ayarları
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Instance Ayarları", "EvolutionApiClient.SetInstanceSettingsAsync",
                inputData: $"{{\"instanceName\":\"{realInstanceName}\",\"groups_ignore\":false,\"always_online\":true}}", outputData: null, status: "OK");
            await _evolutionApiClient.SetInstanceSettingsAsync(realInstanceName);

            // Step: QR Kod alma
            step++;
            var qrBase64 = createResponse.QrCodeData?.Base64 ?? createResponse.QrCodeData?.Code ?? createResponse.QrCodeData?.QrCode;
            var qrSource = "CreateResponse";
            
            if (string.IsNullOrEmpty(qrBase64))
            {
                qrSource = "ConnectEndpoint(polling)";
                await _logService.LogProcessStepAsync(traceId, step, "QR Kod Alma (Polling)", "EvolutionApiClient.GetQRCodeAsync",
                    inputData: $"{{\"instanceName\":\"{realInstanceName}\",\"reason\":\"CreateResponse'da QR yok\"}}", outputData: null, status: "BAŞLADI");
                var qrResponse = await _evolutionApiClient.GetQRCodeAsync(realInstanceName);
                qrBase64 = qrResponse.Base64 ?? qrResponse.Code;
            }

            await _logService.LogProcessStepAsync(traceId, step, "QR Kod Sonuç", "InitializeSessionAsync",
                inputData: null,
                outputData: $"{{\"qrSource\":\"{qrSource}\",\"qrLength\":{qrBase64?.Length ?? 0},\"hasQr\":{(!string.IsNullOrEmpty(qrBase64)).ToString().ToLower()}}}",
                status: string.IsNullOrEmpty(qrBase64) ? "Error" : "OK",
                errorMessage: string.IsNullOrEmpty(qrBase64) ? "QR kodu alınamadı!" : null);

            if (string.IsNullOrEmpty(qrBase64))
            {
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
                throw new InvalidOperationException("QR kodu alınamadı - Evolution API yanıt vermedi");
            }

            // Step: SessionId DB'ye kaydet
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "SessionId DB'ye Kaydet", "ConfigService.UpdateSessionIdAsync",
                inputData: $"{{\"sessionId\":\"{realInstanceName}\"}}", outputData: null, status: "BAŞLADI");
            await _configService.UpdateSessionIdAsync(realInstanceName);

            // Step: Doğrulama
            step++;
            var verifyConfig = await _configService.GetConfigAsync();
            var verified = verifyConfig.SessionId == realInstanceName;
            await _logService.LogProcessStepAsync(traceId, step, "DB Doğrulama", "ConfigService.GetConfigAsync",
                inputData: $"{{\"beklenen\":\"{realInstanceName}\"}}",
                outputData: $"{{\"dbSessionId\":\"{verifyConfig.SessionId ?? "NULL"}\",\"eslesti\":{verified.ToString().ToLower()},\"dbConnectionStatus\":\"{verifyConfig.ConnectionStatus}\"}}",
                status: verified ? "OK" : "UYARI",
                errorMessage: verified ? null : $"SessionId eşleşmedi! Beklenen: {realInstanceName}, Bulunan: {verifyConfig.SessionId}");

            // Step: Tamamlandı
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "InitializeSession TAMAMLANDI", "WhatsAppManager.InitializeSessionAsync",
                inputData: null,
                outputData: $"{{\"sessionName\":\"{realInstanceName}\",\"qrLength\":{qrBase64.Length},\"verified\":{verified.ToString().ToLower()}}}",
                status: "OK");
            
            return new QRCodeDto(qrBase64, realInstanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TRACE:{TraceId}] InitializeSessionAsync HATA", traceId);
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "InitializeSession HATA", "WhatsAppManager.InitializeSessionAsync",
                inputData: null, outputData: null, status: "Error",
                errorMessage: $"{ex.GetType().Name}: {ex.Message}");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
            await _logService.LogFunctionErrorAsync("WHATSAPP_INIT_ERROR", ex, new { SessionName });
            throw;
        }
    }

    public async Task<ConnectionStatus> GetConnectionStatusAsync(string? externalTraceId = null)
    {
        var traceId = externalTraceId ?? Guid.NewGuid().ToString("N")[..8];
        var step = 20; // Controller adımlarından sonra devam et
        try
        {
            _logger.LogInformation("🔍 [TRACE:{TraceId}] ========== GetConnectionStatusAsync BAŞLADI ==========", traceId);
            
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "GetConnectionStatus Giriş", "WhatsAppManager.GetConnectionStatusAsync",
                inputData: null, outputData: null, status: "BAŞLADI");

            // Önce veritabanından kayıtlı SessionId'yi al
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "DB'den SessionId Oku", "ConfigService.GetConfigAsync",
                inputData: null, outputData: null, status: "BAŞLADI");
            var config = await _configService.GetConfigAsync();
            var sessionIdFromDb = config.SessionId;
            await _logService.LogProcessStepAsync(traceId, step, "DB'den SessionId Oku", "ConfigService.GetConfigAsync",
                inputData: null, outputData: $"{{\"sessionId\":\"{sessionIdFromDb ?? "NULL"}\",\"status\":\"{config.ConnectionStatus}\"}}", status: "OK");
            
            // Mevcut session'ları listele
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Instance Listesi Al", "EvolutionApiClient.GetAllInstancesAsync",
                inputData: null, outputData: null, status: "BAŞLADI");
            var instances = await _evolutionApiClient.GetAllInstancesAsync();
            await _logService.LogProcessStepAsync(traceId, step, "Instance Listesi Al", "EvolutionApiClient.GetAllInstancesAsync",
                inputData: null, outputData: $"{{\"count\":{instances.Count},\"instances\":[{string.Join(",", instances.Select(i => $"\"{i}\""))}]}}", status: "OK");
            
            if (instances.Count == 0)
            {
                step++;
                await _logService.LogProcessStepAsync(traceId, step, "Instance Yok, Disconnected", "WhatsAppManager.GetConnectionStatusAsync",
                    inputData: null, outputData: "Hiç aktif session bulunamadı", status: "UYARI");
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
                await _configService.UpdateSessionIdAsync(null); // SessionId'yi temizle
                return ConnectionStatus.Disconnected;
            }

            // Önce veritabanındaki SessionId'yi dene, yoksa ilk aktif session'ı kullan
            string activeSessionName;
            step++;
            if (!string.IsNullOrEmpty(sessionIdFromDb) && instances.Contains(sessionIdFromDb))
            {
                activeSessionName = sessionIdFromDb;
                await _logService.LogProcessStepAsync(traceId, step, "Aktif Session Seçimi", "WhatsAppManager.GetConnectionStatusAsync",
                    inputData: null, outputData: $"{{\"secim\":\"DB_ESLESME\",\"sessionName\":\"{activeSessionName}\"}}", status: "OK");
            }
            else
            {
                // Veritabanındaki session yoksa, cafebot_ ile başlayan ilk instance'ı bul
                activeSessionName = instances.FirstOrDefault(i => i.StartsWith("cafebot_")) ?? instances.First();
                await _logService.LogProcessStepAsync(traceId, step, "Aktif Session Seçimi", "WhatsAppManager.GetConnectionStatusAsync",
                    inputData: null, outputData: $"{{\"secim\":\"ILK_BULUNAN\",\"sessionName\":\"{activeSessionName}\",\"dbSessionId\":\"{sessionIdFromDb ?? "NULL"}\"}}", status: "UYARI");
                
                // Veritabanını güncelle
                await _configService.UpdateSessionIdAsync(activeSessionName);
            }
            
            // Evolution API'den session durumunu al
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Session Durumu Al", "EvolutionApiClient.GetSessionStatusAsync",
                inputData: $"{{\"sessionName\":\"{activeSessionName}\"}}", outputData: null, status: "BAŞLADI");
            var status = await _evolutionApiClient.GetSessionStatusAsync(activeSessionName);
            await _logService.LogProcessStepAsync(traceId, step, "Session Durumu Al", "EvolutionApiClient.GetSessionStatusAsync",
                inputData: null, outputData: $"{{\"state\":\"{status.State ?? "NULL"}\"}}", status: "OK");
            
            // Eğer yeni bağlanmışsa ayarların doğru olduğundan emin ol
            if (status.State == "open")
            {
                 step++;
                 await _logService.LogProcessStepAsync(traceId, step, "Instance Ayarları Güncelle (Open state)", "EvolutionApiClient.SetInstanceSettingsAsync",
                    inputData: $"{{\"sessionName\":\"{activeSessionName}\"}}", outputData: null, status: "OK");
                 await _evolutionApiClient.SetInstanceSettingsAsync(activeSessionName);
            }

            var connectionStatus = status.State switch
            {
                "open" => ConnectionStatus.Connected,
                "connecting" => ConnectionStatus.Connecting,
                "close" => ConnectionStatus.Disconnected,
                _ => ConnectionStatus.Disconnected
            };

            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Durumu DB'ye Yaz", "ConfigService.UpdateConnectionStatusAsync",
                inputData: $"{{\"status\":\"{connectionStatus}\"}}", outputData: null, status: "OK");
            await _configService.UpdateConnectionStatusAsync(connectionStatus);
            
            if (connectionStatus == ConnectionStatus.Connected)
            {
                // Eğer activeSessionName, başta okunan sessionIdFromDb'den farklıysa güncelle
                if (sessionIdFromDb != activeSessionName)
                {
                    step++;
                    await _logService.LogProcessStepAsync(traceId, step, "SessionId Güncelle (Mismatch)", "ConfigService.UpdateSessionIdAsync",
                        inputData: $"{{\"old\":\"{sessionIdFromDb ?? "NULL"}\",\"new\":\"{activeSessionName}\"}}", outputData: null, status: "OK");
                    await _configService.UpdateSessionIdAsync(activeSessionName);
                }
                
                // Webhook'u kaydet — Evolution API mesaj geldiğinde API'mize bildirim yapacak
                try
                {
                    step++;
                    // Docker internal network'te API'nin adresi
                    var webhookUrl = "http://cafebot-api:8080/api/webhook/whatsapp";
                    var securityToken = _configuration["Webhook:SecurityToken"];
                    if (!string.IsNullOrEmpty(securityToken))
                    {
                        webhookUrl += $"?token={securityToken}";
                    }

                    await _logService.LogProcessStepAsync(traceId, step, "Webhook Kaydet", "EvolutionApiClient.SetWebhookAsync",
                        inputData: $"{{\"webhookUrl\":\"{webhookUrl}\"}}", outputData: null, status: "BAŞLADI");
                    await _evolutionApiClient.SetWebhookAsync(activeSessionName, webhookUrl);
                    await _logService.LogProcessStepAsync(traceId, step, "Webhook Kaydet", "EvolutionApiClient.SetWebhookAsync",
                        inputData: null, outputData: "Webhook kaydedildi", status: "OK");
                }
                catch (Exception webhookEx)
                {
                    await _logService.LogProcessStepAsync(traceId, step, "Webhook Kaydet HATA", "EvolutionApiClient.SetWebhookAsync",
                        inputData: null, outputData: null, status: "Error", errorMessage: webhookEx.Message);
                    _logger.LogWarning(webhookEx, "⚠️ [TRACE:{TraceId}] Webhook kaydedilemedi, polling ile devam edilecek.", traceId);
                }
            }
            
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "GetConnectionStatus TAMAMLANDI", "WhatsAppManager.GetConnectionStatusAsync",
                inputData: null, outputData: $"{{\"finalStatus\":\"{connectionStatus}\"}}", status: "OK");
            
            return connectionStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TRACE:{TraceId}] GetConnectionStatusAsync HATA", traceId);
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "GetConnectionStatus HATA", "WhatsAppManager.GetConnectionStatusAsync",
                inputData: null, outputData: null, status: "Error", errorMessage: $"{ex.GetType().Name}: {ex.Message}");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
            await _logService.LogFunctionErrorAsync("WHATSAPP_STATUS_ERROR", ex, new { });
            return ConnectionStatus.Error;
        }
    }

    public async Task<List<GroupDto>> GetGroupsAsync(bool forceRefresh = false)
    {
        try
        {
            if (!forceRefresh && _cache.TryGetValue(GroupsCacheKey, out List<GroupDto>? cachedGroups) && cachedGroups != null)
            {
                _logger.LogInformation("Gruplar önbellekten (cache) getirildi.");
                return cachedGroups;
            }

            // Önce mevcut session'ları listele
            var instances = await _evolutionApiClient.GetAllInstancesAsync();
            
            if (instances.Count == 0)
            {
                _logger.LogWarning("Hiç aktif session bulunamadı. Gruplar alınamıyor.");
                return new List<GroupDto>();
            }

            var activeSessionName = instances.First();
            _logger.LogInformation("Gruplar alınıyor. Session: {SessionName}", activeSessionName);

            // Önce ayarların doğru olduğundan emin ol (grupları getirmesi için)
            await _evolutionApiClient.SetInstanceSettingsAsync(activeSessionName);

            var groupsResponse = await _evolutionApiClient.GetGroupsAsync(activeSessionName);
            var groups = groupsResponse
                .Where(g => !string.IsNullOrEmpty(g.Id) && !string.IsNullOrEmpty(g.Subject))
                .Select(g => new GroupDto(
                    Id: g.Id!, 
                    Name: CleanGroupName(g.Subject!), // Emoji ve özel karakterleri temizle
                    ParticipantCount: g.Participants?.Count ?? 0))
                .ToList();

            // Eğer grup gelmezse ve WhatsApp bağlıysa, instance'ı senkronize olmaya zorlamak için restart atıp tekrar dene
            if (groups.Count == 0)
            {
                _logger.LogWarning("Hiç grup bulunamadı. WhatsApp senkronizasyonu için instance yeniden başlatılıyor...");
                await _evolutionApiClient.RestartInstanceAsync(activeSessionName);
                
                // Senkronizasyon için yeterli süre bekle
                await Task.Delay(15000);
                
                groupsResponse = await _evolutionApiClient.GetGroupsAsync(activeSessionName);
                groups = groupsResponse
                    .Where(g => !string.IsNullOrEmpty(g.Id) && !string.IsNullOrEmpty(g.Subject))
                    .Select(g => new GroupDto(
                        Id: g.Id!, 
                        Name: CleanGroupName(g.Subject!), // Emoji ve özel karakterleri temizle
                        ParticipantCount: g.Participants?.Count ?? 0))
                    .ToList();
            }

            if (groups.Count > 0)
            {
                var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                };
                _cache.Set(GroupsCacheKey, groups, cacheOptions);
            }

            _logger.LogDebug("{Count} grup alındı. Session: {SessionName}", groups.Count, activeSessionName);
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gruplar alınamadı.");
            await _logService.LogFunctionErrorAsync("WHATSAPP_GROUPS_ERROR", ex, new { });
            return new List<GroupDto>();
        }
    }

    public async Task<bool> SendMessageAsync(string groupId, string message)
    {
        if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("Group ID boş olamaz", nameof(groupId));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Mesaj boş olamaz", nameof(message));

        try
        {
            // Önce veritabanından kayıtlı SessionId'yi kontrol et
            var config = await _configService.GetConfigAsync();
            var sessionIdFromDb = config.SessionId;

            // Mevcut session'ları listele
            var instances = await _evolutionApiClient.GetAllInstancesAsync();
            
            if (instances.Count == 0)
            {
                _logger.LogWarning("Hiç aktif session bulunamadı - Evolution API'de instance yok");
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
                return false;
            }

            // Önce veritabanındaki SessionId'yi dene, yoksa ilk aktif session'ı kullan
            string activeSessionName;
            if (!string.IsNullOrEmpty(sessionIdFromDb) && instances.Contains(sessionIdFromDb))
            {
                activeSessionName = sessionIdFromDb;
                _logger.LogInformation("Veritabanındaki SessionId kullanılıyor: {SessionName}", activeSessionName);
            }
            else
            {
                activeSessionName = instances.First();
                _logger.LogInformation("Veritabanındaki SessionId ({DbSessionId}) bulunamadı, ilk aktif session kullanılıyor: {SessionName}", 
                    sessionIdFromDb, activeSessionName);
                
                // Veritabanını güncelle
                await _configService.UpdateSessionIdAsync(activeSessionName);
            }

            return await SendMessageWithInstanceAsync(activeSessionName, groupId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj gönderilirken hata. GroupId: {GroupId}, Error: {Error}", groupId, ex.Message);
            
            // Session hatası varsa connection status'ü güncelle
            if (ex.Message.Contains("SessionError") || ex.Message.Contains("No sessions") || ex.Message.Contains("404"))
            {
                _logger.LogWarning("Session hatası tespit edildi - bağlantı durumu Disconnected olarak güncelleniyor");
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
            }
            
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(string instanceName, string groupId, string message, string? traceId = null)
    {
        return await SendMessageWithInstanceAsync(instanceName, groupId, message, traceId);
    }

    private async Task<bool> SendMessageWithInstanceAsync(string instanceName, string groupId, string message, string? traceId = null)
    {
        var currentTraceId = traceId ?? $"send-{DateTime.UtcNow.Ticks}";
        
        try
        {
            _logger.LogInformation("Mesaj gönderiliyor. Instance: {InstanceName}, GroupId: {GroupId}, Message: {Message}", 
                instanceName, groupId, message);

            // TRACE LOG: API'ye gönderilecek veri
            await _logService.LogProcessStepAsync(
                traceId: currentTraceId,
                stepOrder: 91,
                stepName: "SendMessage.ApiCall",
                functionName: "WhatsAppManager.SendMessageWithInstanceAsync",
                inputData: $"{{\"instanceName\":\"{instanceName}\",\"groupId\":\"{groupId}\",\"message\":\"{message}\"}}",
                outputData: null,
                status: "OK");

            SendMessageResponse response;
            try 
            {
                response = await _evolutionApiClient.SendTextMessageAsync(instanceName, groupId, message);
            }
            catch (Exception ex) when (ex.Message.Contains("SessionError") || ex.Message.Contains("No sessions"))
            {
                _logger.LogWarning("Evolution 'No sessions' hatası verdi. Oturumu tazeleyip (restart) tekrar deneniyor...");
                
                // TRACE LOG: İlk hata
                await _logService.LogProcessStepAsync(
                    traceId: currentTraceId,
                    stepOrder: 92,
                    stepName: "SendMessage.Retry",
                    functionName: "WhatsAppManager.SendMessageWithInstanceAsync",
                    inputData: null,
                    outputData: null,
                    status: "Skip",
                    errorMessage: "SessionError: No sessions - Oturum tazeleniyor...");

                // Evolution'a oturumu yeniden başlatmasını söyle (restartInstance)
                try { await _evolutionApiClient.RestartInstanceAsync(instanceName); } catch { /* ignore */ }
                
                // Oturumun açılmasını bekle (max 20 saniye)
                bool isOpen = false;
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(2000); // 2 saniye bekle
                    var status = await _evolutionApiClient.GetSessionStatusAsync(instanceName);
                    
                    await _logService.LogProcessStepAsync(
                        traceId: currentTraceId,
                        stepOrder: 93,
                        stepName: "SendMessage.WaitStatus",
                        functionName: "WhatsAppManager.SendMessageWithInstanceAsync",
                        inputData: $"{{ \"attempt\": {i + 1} }}",
                        outputData: $"{{ \"state\": \"{status.State}\" }}",
                        status: "OK");

                    if (status.State == "open")
                    {
                        isOpen = true;
                        break;
                    }
                }

                if (!isOpen)
                {
                    throw new InvalidOperationException("Oturum restart sonrası 'open' durumuna gelmedi.");
                }
                
                // EKSTRA BEKLEME: 'open' olduktan sonra bile id'nin oturması için birkaç saniye daha ver
                await _logService.LogProcessStepAsync(
                    traceId: currentTraceId,
                    stepOrder: 94,
                    stepName: "SendMessage.SettleDelay",
                    functionName: "WhatsAppManager.SendMessageWithInstanceAsync",
                    inputData: null,
                    outputData: null,
                    status: "OK",
                    errorMessage: "Oturum açık, son senkronizasyon için 5 saniye bekleniyor...");
                
                await Task.Delay(5000);
                
                // İkinci kez dene
                response = await _evolutionApiClient.SendTextMessageAsync(instanceName, groupId, message);
            }
            
            var success = response.Key?.Id != null;

            // TRACE LOG: API sonucu
            await _logService.LogProcessStepAsync(
                traceId: currentTraceId,
                stepOrder: 95,
                stepName: "SendMessage.Result",
                functionName: "WhatsAppManager.SendMessageWithInstanceAsync",
                inputData: null,
                outputData: $"{{\"success\":{success.ToString().ToLower()},\"keyId\":\"{response.Key?.Id}\",\"status\":\"{response.Status}\"}}",
                status: success ? "OK" : "Error",
                errorMessage: success ? null : $"Key.Id null. Status: {response.Status}");

            if (success)
            {
                _logger.LogInformation("✅ Mesaj başarıyla gönderildi. Instance: {InstanceName}, GroupId: {GroupId}", 
                    instanceName, groupId);
            }
            else
            {
                _logger.LogWarning("Mesaj gönderilemedi. Status: {Status}, Instance: {InstanceName}, GroupId: {GroupId}", 
                    response.Status, instanceName, groupId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj gönderilirken hata. Instance: {InstanceName}, GroupId: {GroupId}, Error: {Error}", 
                instanceName, groupId, ex.Message);
            
            // TRACE LOG: Hata detayı
            await _logService.LogProcessStepAsync(
                traceId: currentTraceId,
                stepOrder: 99,
                stepName: "SendMessage.Exception",
                functionName: "WhatsAppManager.SendMessageWithInstanceAsync",
                inputData: $"{{\"instanceName\":\"{instanceName}\",\"groupId\":\"{groupId}\"}}",
                outputData: null,
                status: "Error",
                errorMessage: $"{ex.GetType().Name}: {ex.Message}");

            // Session hatası varsa connection status'ü güncelle
            if (ex.Message.Contains("SessionError") || ex.Message.Contains("No sessions") || ex.Message.Contains("404"))
            {
                _logger.LogWarning("Session hatası düzelmedi - bağlantı durumu Disconnected olarak güncelleniyor");
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
            }
            
            return false;
        }
    }

    public async Task<List<string>> GetAllInstancesAsync()
    {
        try
        {
            return await _evolutionApiClient.GetAllInstancesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instance listesi alınamadı");
            return new List<string>();
        }
    }

    public async Task DisconnectAsync(string? externalTraceId = null)
    {
        var traceId = externalTraceId ?? Guid.NewGuid().ToString("N")[..8];
        var step = 30; // Disconnect adımları
        try
        {
            _logger.LogInformation("WhatsApp oturumu kapatılıyor. TraceId: {TraceId}", traceId);
            
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Disconnect Giriş", "WhatsAppManager.DisconnectAsync",
                inputData: null, outputData: null, status: "BAŞLADI");

            // Veritabanından aktif SessionId'yi al
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "DB'den SessionId Oku", "ConfigService.GetConfigAsync",
                inputData: null, outputData: null, status: "BAŞLADI");
            var config = await _configService.GetConfigAsync();
            var sessionToDelete = config.SessionId ?? SessionName;
            await _logService.LogProcessStepAsync(traceId, step, "DB'den SessionId Oku", "ConfigService.GetConfigAsync",
                inputData: null, outputData: $"{{\"sessionToDelete\":\"{sessionToDelete}\"}}", status: "OK");
            
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Evolution API Silme", "EvolutionApiClient.DeleteSessionAsync",
                inputData: $"{{\"sessionName\":\"{sessionToDelete}\"}}", outputData: null, status: "BAŞLADI");
            await _evolutionApiClient.DeleteSessionAsync(sessionToDelete);
            await _logService.LogProcessStepAsync(traceId, step, "Evolution API Silme", "EvolutionApiClient.DeleteSessionAsync",
                inputData: null, outputData: "Session silindi", status: "OK");

            step++;
            await _logService.LogProcessStepAsync(traceId, step, "DB Durum Güncelle", "ConfigService.UpdateConnectionStatusAsync",
                inputData: $"{{\"newStatus\":\"Disconnected\"}}", outputData: null, status: "BAŞLADI");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
            await _configService.UpdateSessionIdAsync(null); // SessionId'yi temizle
            await _logService.LogProcessStepAsync(traceId, step, "DB Durum Güncelle", "ConfigService.UpdateConnectionStatusAsync",
                inputData: null, outputData: "DB temizlendi", status: "OK");
            
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Disconnect TAMAMLANDI", "WhatsAppManager.DisconnectAsync",
                inputData: null, outputData: null, status: "OK");

            _logger.LogInformation("WhatsApp oturumu kapatıldı. Session: {SessionName}", sessionToDelete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp oturumu kapatılırken hata. TraceId: {TraceId}", traceId);
            step++;
            await _logService.LogProcessStepAsync(traceId, step, "Disconnect HATA", "WhatsAppManager.DisconnectAsync",
                inputData: null, outputData: null, status: "Error", errorMessage: ex.Message);
            await _logService.LogFunctionErrorAsync("WHATSAPP_DISCONNECT_ERROR", ex, new { SessionName });
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
            throw;
        }
    }

    private static ConnectionStatus MapEvolutionStateToConnectionStatus(string? state) =>
        state?.ToLower() switch
        {
            "open"       => ConnectionStatus.Connected,
            "connecting" => ConnectionStatus.Connecting,
            "close"      => ConnectionStatus.Disconnected,
            "closed"     => ConnectionStatus.Disconnected,
            _            => ConnectionStatus.Disconnected
        };

    /// <summary>
    /// Grup ismindeki emoji ve özel karakterleri temizler
    /// </summary>
    private static string CleanGroupName(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return groupName;

        // Emoji ve özel karakterleri kaldır, sadece harf, rakam, boşluk ve temel noktalama bırak
        var cleaned = System.Text.RegularExpressions.Regex.Replace(groupName, @"[^\w\s\-\.\(\)]", "");
        
        // Çoklu boşlukları tek boşluğa çevir
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
        
        return cleaned.Trim();
    }
}
