using CafeBot.Business.DTOs;
using CafeBot.Business.Infrastructure.WhatsApp;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using Microsoft.Extensions.Logging;

namespace CafeBot.Business.Services;

public class WhatsAppManager : IWhatsAppService
{
    private readonly EvolutionApiClient _evolutionApiClient;
    private readonly IConfigService _configService;
    private readonly ILogService _logService;
    private readonly ILogger<WhatsAppManager> _logger;
    private const string SessionName = "cafebot";

    public WhatsAppManager(
        EvolutionApiClient evolutionApiClient,
        IConfigService configService,
        ILogService logService,
        ILogger<WhatsAppManager> logger)
    {
        _evolutionApiClient = evolutionApiClient ?? throw new ArgumentNullException(nameof(evolutionApiClient));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QRCodeDto> InitializeSessionAsync()
    {
        try
        {
            _logger.LogInformation("WhatsApp oturumu başlatılıyor.");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Connecting);

            // Önce mevcut session'ı sil (varsa) - böylece taze QR kodu alırız
            await _evolutionApiClient.DeleteSessionAsync(SessionName);
            await Task.Delay(1000); // Silme işleminin tamamlanması için bekle

            // Yeni session oluştur
            var createResponse = await _evolutionApiClient.CreateSessionAsync(SessionName);

            // Evolution API'den dönen GERÇEK instance name'i al
            var realInstanceName = createResponse.InstanceData?.InstanceName ?? SessionName;
            _logger.LogInformation("Evolution API'den gelen gerçek instance name: {RealInstanceName}", realInstanceName);

            // Ayarları güncelle (Grupları yoksaymayı kapat, mesaj okumayı aç)
            await _evolutionApiClient.SetInstanceSettingsAsync(realInstanceName);

            // QR kodu create response'undan al (v1/v2 formatı - base64, code veya qrcode alanlarından)
            var qrBase64 = createResponse.QrCodeData?.Base64
                ?? createResponse.QrCodeData?.Code
                ?? createResponse.QrCodeData?.QrCode;

            // Create response'da yoksa connect endpoint'inden polling ile al
            if (string.IsNullOrEmpty(qrBase64))
            {
                var qrResponse = await _evolutionApiClient.GetQRCodeAsync(realInstanceName);
                qrBase64 = qrResponse.Base64 ?? qrResponse.Code;
            }

            if (string.IsNullOrEmpty(qrBase64))
            {
                _logger.LogError("QR kodu alınamadı. Session: {Session}", realInstanceName);
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
                throw new InvalidOperationException("QR kodu alınamadı - Evolution API yanıt vermedi");
            }

            _logger.LogInformation("QR kodu başarıyla alındı. Session: {Session}", realInstanceName);
            
            // GERÇEK instance name'i veritabanına kaydet (hardcoded değil!)
            await _configService.UpdateSessionIdAsync(realInstanceName);
            
            return new QRCodeDto(qrBase64, realInstanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp oturumu başlatılırken hata.");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
            await _logService.LogFunctionErrorAsync("WHATSAPP_INIT_ERROR", ex, new { SessionName });
            throw;
        }
    }

    public async Task<ConnectionStatus> GetConnectionStatusAsync()
    {
        try
        {
            // Önce mevcut session'ları listele
            var instances = await _evolutionApiClient.GetAllInstancesAsync();
            
            if (instances.Count == 0)
            {
                _logger.LogDebug("Hiç aktif session bulunamadı.");
                await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
                await _configService.UpdateSessionIdAsync(null); // SessionId'yi temizle
                return ConnectionStatus.Disconnected;
            }

            // İlk aktif session'ı kontrol et
            var activeSessionName = instances.First();
            _logger.LogDebug("Aktif session bulundu: {SessionName}", activeSessionName);
            
            var status = await _evolutionApiClient.GetSessionStatusAsync(activeSessionName);
            
            // Eğer yeni bağlanmışsa ayarların doğru olduğundan emin ol
            if (status.State == "open")
            {
                 await _evolutionApiClient.SetInstanceSettingsAsync(activeSessionName);
            }

            var connectionStatus = status.State switch
            {
                "open" => ConnectionStatus.Connected,
                "connecting" => ConnectionStatus.Connecting,
                "close" => ConnectionStatus.Disconnected,
                _ => ConnectionStatus.Disconnected
            };

            _logger.LogDebug("Bağlantı durumu: {Status} (Session: {SessionName})", connectionStatus, activeSessionName);
            await _configService.UpdateConnectionStatusAsync(connectionStatus);
            
            if (connectionStatus == ConnectionStatus.Connected)
            {
                // Veritabanındaki SessionId'yi gerçek aktif instance name ile güncelle
                var config = await _configService.GetConfigAsync();
                if (config.SessionId != activeSessionName)
                {
                    _logger.LogInformation("SessionId güncelleniyor: {OldSessionId} → {NewSessionId}", 
                        config.SessionId, activeSessionName);
                    await _configService.UpdateSessionIdAsync(activeSessionName);
                }
                
                // Webhook'u kaydet — Evolution API mesaj geldiğinde API'mize bildirim yapacak
                try
                {
                    // Docker internal network'te API'nin adresi
                    var webhookUrl = "http://cafebot-api:8080/api/webhook/whatsapp";
                    await _evolutionApiClient.SetWebhookAsync(activeSessionName, webhookUrl);
                    _logger.LogInformation("Webhook kaydedildi: {Url} (Session: {SessionName})", webhookUrl, activeSessionName);
                }
                catch (Exception webhookEx)
                {
                    _logger.LogWarning(webhookEx, "Webhook kaydedilemedi, polling ile devam edilecek.");
                }
            }
            else
            {
                // SessionId'yi SİLME — geçici kesintide tekrar kullanılabilir
                // Sadece ConnectionStatus güncelleniyor, SessionId korunuyor
            }
            
            return connectionStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı durumu alınamadı.");
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Error);
            await _logService.LogFunctionErrorAsync("WHATSAPP_STATUS_ERROR", ex, new { });
            return ConnectionStatus.Error;
        }
    }

    public async Task<List<GroupDto>> GetGroupsAsync()
    {
        try
        {
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

    public async Task DisconnectAsync()
    {
        try
        {
            _logger.LogInformation("WhatsApp oturumu kapatılıyor.");
            
            // Veritabanından aktif SessionId'yi al
            var config = await _configService.GetConfigAsync();
            var sessionToDelete = config.SessionId ?? SessionName;
            
            await _evolutionApiClient.DeleteSessionAsync(sessionToDelete);
            await _configService.UpdateConnectionStatusAsync(ConnectionStatus.Disconnected);
            await _configService.UpdateSessionIdAsync(null); // SessionId'yi temizle
            
            _logger.LogInformation("WhatsApp oturumu kapatıldı. Session: {SessionName}", sessionToDelete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp oturumu kapatılırken hata.");
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
