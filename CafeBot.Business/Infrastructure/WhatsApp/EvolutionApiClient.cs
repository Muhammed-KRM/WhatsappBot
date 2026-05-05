using System.Net.Http.Json;

namespace CafeBot.Business.Infrastructure.WhatsApp;

/// <summary>
/// Evolution API v1 client - QR kodu ve WhatsApp entegrasyonu için
/// </summary>
public class EvolutionApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public EvolutionApiClient(HttpClient httpClient, string baseUrl, string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);
    }

    /// <summary>
    /// Session oluşturur. Zaten varsa mevcut session'ı döndürür.
    /// JsonDocument ile güvenli parse — API yanıt yapısı değişse de kırılmaz.
    /// </summary>
    public async Task<CreateSessionResponse> CreateSessionAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        // Önce mevcut session'ı kontrol et
        try
        {
            var existingStatus = await GetSessionStatusAsync(sessionName);
            if (existingStatus.State == "open")
            {
                return new CreateSessionResponse { InstanceName = sessionName, StatusInfo = "existing" };
            }

            // Eğer kapalıysa önce sil, sonra yeniden oluştur
            if (existingStatus.State == "close")
            {
                await DeleteSessionAsync(sessionName);
                await Task.Delay(2000); // API'nin temizlemesi için bekle
            }
        }
        catch (Exception ex)
        {
            // Session durumu alınamazsa devam et (yeni session oluştur)
            Console.WriteLine($"[EvolutionApiClient] Session status check failed: {ex.Message}");
        }

        // V1 request format
        var request = new { instanceName = sessionName, qrcode = true };
        var response = await _httpClient.PostAsJsonAsync("/instance/create", request);

        // Session zaten var → mevcut session'ı kullan
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            if (errorContent.Contains("already in use"))
            {
                return new CreateSessionResponse { InstanceName = sessionName, StatusInfo = "existing" };
            }
            throw new HttpRequestException($"Failed to create session: {response.StatusCode} - {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync();

        // JsonDocument ile güvenli parse
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = new CreateSessionResponse { InstanceName = sessionName, StatusInfo = "created" };

        // instance objesini parse et
        if (root.TryGetProperty("instance", out var instanceEl) && instanceEl.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            result.InstanceData = new CreateSessionInstance
            {
                InstanceName = instanceEl.TryGetProperty("instanceName", out var nameEl) ? nameEl.GetString() : null,
                InstanceId = instanceEl.TryGetProperty("instanceId", out var idEl) ? idEl.GetString() : null,
                Status = instanceEl.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null,
                Hash = instanceEl.TryGetProperty("hash", out var hashEl) && hashEl.ValueKind == System.Text.Json.JsonValueKind.String ? hashEl.GetString() : 
                       instanceEl.TryGetProperty("hash", out var hashEl2) ? hashEl2.GetRawText() : null,
                ConnectionStatus = instanceEl.TryGetProperty("connectionStatus", out var connEl) ? connEl.GetString() : null
            };
        }

        // qrcode objesini parse et
        if (root.TryGetProperty("qrcode", out var qrEl) && qrEl.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            result.QrCodeData = new CreateSessionQrCode
            {
                Base64 = qrEl.TryGetProperty("base64", out var b64El) ? b64El.GetString() : null,
                Code = qrEl.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null,
                QrCode = qrEl.TryGetProperty("qrcode", out var qrCodeEl) ? qrCodeEl.GetString() : null,
                Count = qrEl.TryGetProperty("count", out var countEl) && countEl.TryGetInt32(out var count) ? count : null
            };
        }

        return result;
    }

    /// <summary>
    /// V1: QR kodu /instance/connect/:instance endpoint'inden alır
    /// </summary>
    public async Task<QRCodeResponse> GetQRCodeAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        // V1'de QR kodu /instance/connect/:instance endpoint'inden geliyor
        // Birkaç kez dene (max 15 saniye)
        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(3000);

            var response = await _httpClient.GetAsync($"/instance/connect/{sessionName}");
            if (!response.IsSuccessStatusCode) continue;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) continue;

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? base64 = null;
            string? code = null;

            // QR verisi üst seviyede veya qrcode/base64 içinde olabilir
            if (root.TryGetProperty("base64", out var b64El)) base64 = b64El.GetString();
            if (root.TryGetProperty("code", out var codeEl)) code = codeEl.GetString();
            if (root.TryGetProperty("qrcode", out var qrEl))
            {
                if (qrEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    base64 ??= qrEl.GetString();
                else if (qrEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (qrEl.TryGetProperty("base64", out var innerB64)) base64 ??= innerB64.GetString();
                    if (qrEl.TryGetProperty("qrcode", out var innerQr)) base64 ??= innerQr.GetString();
                }
            }

            if (base64 != null && base64.Length > 10)
                return new QRCodeResponse { Base64 = base64, Code = code ?? base64 };
            if (code != null && code.Length > 10)
                return new QRCodeResponse { Base64 = code, Code = code };
        }

        return new QRCodeResponse();
    }

    /// <summary>
    /// V1: Bağlantı durumunu /instance/connectionState/:instance endpoint'inden alır
    /// Hızlı timeout ile - session yoksa hemen fail etsin
    /// v1.8.6+ uyumlu - farklı response formatlarını destekler
    /// </summary>
    public async Task<SessionStatusResponse> GetSessionStatusAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        try
        {
            // Hızlı timeout - session yoksa hemen anlaşılsın
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync($"/instance/connectionState/{sessionName}", cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EvolutionApiClient] Session status check failed: {response.StatusCode}");
                return new SessionStatusResponse { State = "close" };
            }

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[EvolutionApiClient] Raw connectionState response: {json}");

            // V1 response: {"instance":{"instanceName":"...","state":"open"}}
            // v1.8.6+ response: {"state":"open"} veya {"instance":{"connectionStatus":"open"}}
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? state = null;

            // Önce direkt state field'ı dene (v1.8.6+ yeni format)
            if (root.TryGetProperty("state", out var directState))
            {
                state = directState.GetString();
                Console.WriteLine($"[EvolutionApiClient] Found direct state: {state}");
            }
            // instance.state veya instance.connectionStatus dene (eski format)
            else if (root.TryGetProperty("instance", out var instanceEl))
            {
                if (instanceEl.TryGetProperty("state", out var stateEl))
                {
                    state = stateEl.GetString();
                    Console.WriteLine($"[EvolutionApiClient] Found instance.state: {state}");
                }
                else if (instanceEl.TryGetProperty("connectionStatus", out var connEl))
                {
                    state = connEl.GetString();
                    Console.WriteLine($"[EvolutionApiClient] Found instance.connectionStatus: {state}");
                }
            }

            // State mapping - farklı sürümlerde farklı değerler olabilir
            // "open" = bağlı, "close" = bağlı değil, "connecting" = bağlanıyor
            if (state != null)
            {
                state = state.ToLower();
                // "open" veya "connected" = bağlı
                if (state == "connected") state = "open";
            }

            Console.WriteLine($"[EvolutionApiClient] Session {sessionName} final state: {state ?? "unknown"}");
            return new SessionStatusResponse { State = state ?? "close" };
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[EvolutionApiClient] Session status check timeout for {sessionName}");
            return new SessionStatusResponse { State = "close" };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EvolutionApiClient] Session status check error: {ex.Message}");
            return new SessionStatusResponse { State = "close" };
        }
    }

    /// <summary>
    /// Tüm grupları listeler — boş yanıt ve farklı JSON yapılarına dayanıklı
    /// Birden fazla endpoint dener ve bağlantı durumunu kontrol eder
    /// v1.8.6 uyumlu - fetchAllGroups yerine daha güvenli endpoint kullanır
    /// </summary>
    public async Task<List<GroupResponse>> GetGroupsAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        // Önce bağlantı durumunu kontrol et
        try
        {
            var status = await GetSessionStatusAsync(sessionName);
            if (status.State != "open")
            {
                Console.WriteLine($"[EvolutionApiClient] Session not connected: {status.State}. Gruplar alınamıyor.");
                return new List<GroupResponse>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EvolutionApiClient] Session status check failed: {ex.Message}");
            // Devam et, belki grup listeleme çalışır
        }

        // v1.8.6'da fetchAllGroups hata veriyor ("groups is not iterable")
        // Daha güvenli endpoint'leri kullan
        var endpoints = new[]
        {
            // v1.8.6'da çalışan endpoint (POST ile)
            $"/chat/findChats/{sessionName}",
            // Fallback: Eski endpoint (GET ile, ama hata verebilir)
            $"/instance/fetchGroups/{sessionName}"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                Console.WriteLine($"[EvolutionApiClient] Trying endpoint: {endpoint}");
                
                HttpResponseMessage response;
                if (endpoint.Contains("/chat/findChats"))
                {
                    // POST request with filter for groups only
                    var requestBody = new
                    {
                        where = new
                        {
                            id = new { _regex = "@g.us$" } // Sadece gruplar (group JID'leri @g.us ile biter)
                        }
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                else
                {
                    // GET request
                    response = await _httpClient.GetAsync(endpoint);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(responseJson))
                    {
                        var groups = ParseGroupsFromJson(responseJson);
                        if (groups.Count > 0)
                        {
                            Console.WriteLine($"[EvolutionApiClient] Successfully fetched {groups.Count} groups from {endpoint}");
                            return groups;
                        }
                        else
                        {
                            Console.WriteLine($"[EvolutionApiClient] Endpoint {endpoint} returned 0 groups");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[EvolutionApiClient] Endpoint {endpoint} failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EvolutionApiClient] Endpoint {endpoint} error: {ex.Message}");
                continue; // Sonraki endpoint'i dene
            }
        }

        Console.WriteLine("[EvolutionApiClient] All endpoints failed or returned 0 groups");
        return new List<GroupResponse>();
    }

    /// <summary>
    /// JSON'dan grup listesini parse eder - farklı formatları destekler
    /// v1.8.6 uyumlu - chat/findChats response formatını da destekler
    /// </summary>
    private List<GroupResponse> ParseGroupsFromJson(string json)
    {
        var groups = new List<GroupResponse>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Doğrudan array ise
            System.Text.Json.JsonElement arrayElement;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                arrayElement = root;
            }
            // Bazı versiyonlarda wrapper obje olabilir
            else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                // Olası wrapper key'leri dene
                if (root.TryGetProperty("groups", out var groupsEl) && groupsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    arrayElement = groupsEl;
                else if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    arrayElement = dataEl;
                else if (root.TryGetProperty("chats", out var chatsEl) && chatsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    arrayElement = chatsEl; // v1.8.6 chat/findChats response
                else
                    return groups;
            }
            else
            {
                return groups;
            }

            foreach (var item in arrayElement.EnumerateArray())
            {
                // Grup ID'sini al (farklı field isimleri olabilir)
                string? groupId = null;
                if (item.TryGetProperty("id", out var idEl))
                    groupId = idEl.GetString();
                else if (item.TryGetProperty("remoteJid", out var jidEl))
                    groupId = jidEl.GetString();
                
                // Sadece grup JID'lerini al (@g.us ile bitenler)
                if (string.IsNullOrEmpty(groupId) || !groupId.EndsWith("@g.us"))
                    continue;

                // Grup ismini al (farklı field isimleri olabilir)
                string? groupName = null;
                if (item.TryGetProperty("subject", out var subEl))
                    groupName = subEl.GetString();
                else if (item.TryGetProperty("name", out var nameEl))
                    groupName = nameEl.GetString();
                else if (item.TryGetProperty("pushName", out var pushEl))
                    groupName = pushEl.GetString();

                var group = new GroupResponse
                {
                    Id = groupId,
                    Subject = groupName ?? groupId // Eğer isim yoksa ID'yi kullan
                };

                groups.Add(group);
            }

            if (groups.Count == 0)
            {
                // Log the raw JSON if empty for debugging (ilk 500 karakter)
                var jsonPreview = json.Length > 500 ? json.Substring(0, 500) + "..." : json;
                Console.WriteLine($"[EvolutionApiClient] Parse returned 0 groups. Raw JSON preview: {jsonPreview}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EvolutionApiClient] JSON parsing error: {ex.Message}");
        }

        return groups;
    }

    /// <summary>
    /// Mesaj gönderir - Evolution API'nin DOĞRU formatını kullanır
    /// Bu versiyonda textMessage wrapper gerekiyor
    /// </summary>
    public async Task<SendMessageResponse> SendTextMessageAsync(string sessionName, string groupId, string text)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));
        if (string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("Group ID cannot be empty", nameof(groupId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        // Evolution API'nin bu versiyonunda textMessage wrapper gerekiyor
        var request = new 
        { 
            number = groupId, 
            textMessage = new { text = text }
        };
        
        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        var url = $"/message/sendText/{sessionName}";
        
        try
        {
            Console.WriteLine($"[EvolutionApiClient] ── TRACE START ──");
            Console.WriteLine($"[EvolutionApiClient] URL: POST {url}");
            Console.WriteLine($"[EvolutionApiClient] Request Body: {requestJson}");
            Console.WriteLine($"[EvolutionApiClient] Session: {sessionName}, GroupId: {groupId}, Text: {text}");
            
            var response = await _httpClient.PostAsJsonAsync(url, request);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[EvolutionApiClient] Response Status: {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine($"[EvolutionApiClient] Response Body: {responseBody}");
            Console.WriteLine($"[EvolutionApiClient] Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}"))}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EvolutionApiClient] ❌ FAILED: {response.StatusCode} - {responseBody}");
                Console.WriteLine($"[EvolutionApiClient] ── TRACE END ──");
                
                // Özel hata durumları
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException($"Session bulunamadı: {sessionName}");
                }
                else if (responseBody.Contains("SessionError") || responseBody.Contains("No sessions"))
                {
                    throw new InvalidOperationException($"Session hatası: {responseBody}");
                }
                
                throw new HttpRequestException($"Send message failed {(int)response.StatusCode}: {responseBody}");
            }

            // Başarılı yanıtı parse et
            SendMessageResponse? result = null;
            try
            {
                result = System.Text.Json.JsonSerializer.Deserialize<SendMessageResponse>(responseBody, 
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Console.WriteLine($"[EvolutionApiClient] ✅ Parsed: Key.Id={result?.Key?.Id}, Status={result?.Status}");
            }
            catch (Exception parseEx)
            {
                Console.WriteLine($"[EvolutionApiClient] ⚠️ Parse error: {parseEx.Message}");
                Console.WriteLine($"[EvolutionApiClient] Raw response was: {responseBody}");
            }
            
            Console.WriteLine($"[EvolutionApiClient] ── TRACE END ──");
            return result ?? new SendMessageResponse { Status = "success" };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EvolutionApiClient] ❌ Exception: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[EvolutionApiClient] ── TRACE END ──");
            throw;
        }
    }

    /// <summary>
    /// Instance ayarlarını günceller (Mesaj okuma, grupları takip etme)
    /// </summary>
    public async Task<bool> SetInstanceSettingsAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        var settings = new
        {
            reject_call = false,
            msg_call = "",
            groups_ignore = false,
            always_online = true,
            read_messages = true,
            read_status = false,
            sync_full_history = false
        };

        var response = await _httpClient.PostAsJsonAsync($"/settings/set/{sessionName}", settings);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Instance'ı yeniden başlatır (Senkronizasyonları tetiklemek için)
    /// </summary>
    public async Task<bool> RestartInstanceAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        var response = await _httpClient.PutAsync($"/instance/restart/{sessionName}", null);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Webhook kaydeder - Evolution API mesaj geldiğinde bu URL'ye POST yapar.
    /// </summary>
    public async Task<bool> SetWebhookAsync(string sessionName, string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("Webhook URL cannot be empty", nameof(webhookUrl));

        var request = new
        {
            url = webhookUrl,
            webhook_by_events = false,
            webhook_base64 = false,
            events = new[]
            {
                "MESSAGES_UPSERT"
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"/webhook/set/{sessionName}", request);
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[EvolutionApiClient] Webhook başarıyla kaydedildi: {webhookUrl}");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[EvolutionApiClient] Webhook kaydedilemedi: {(int)response.StatusCode} - {error}");
        }
        
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Tüm mevcut instance'ları listeler - doğru format
    /// </summary>
    public async Task<List<string>> GetAllInstancesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/instance/fetchInstances");
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EvolutionApiClient] Fetch instances failed: {response.StatusCode}");
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var instances = new List<string>();

            // Response format: array of objects with "instance" property
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.TryGetProperty("instance", out var instanceEl))
                    {
                        if (instanceEl.TryGetProperty("instanceName", out var nameEl))
                        {
                            var name = nameEl.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                instances.Add(name);
                                Console.WriteLine($"[EvolutionApiClient] Found instance: {name}");
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"[EvolutionApiClient] Total instances found: {instances.Count}");
            return instances;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EvolutionApiClient] GetAllInstancesAsync error: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Session siler
    /// </summary>
    public async Task<bool> DeleteSessionAsync(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        var response = await _httpClient.DeleteAsync($"/instance/delete/{sessionName}");
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Mesajları polling ile alır
    /// </summary>
    public async Task<List<EvolutionMessage>> GetMessagesAsync(string sessionName, string remoteJid)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        // V1.8.2 standard endpoint: POST /chat/findMessages/{instance}
        var url = $"/chat/findMessages/{sessionName}";
        
        var request = new
        {
            where = new
            {
                key = new
                {
                    remoteJid = remoteJid
                }
            },
            count = 50
        };

        var response = await _httpClient.PostAsJsonAsync(url, request);

        if (!response.IsSuccessStatusCode)
        {
            // Fallback: Bazı versiyonlarda /message/findMessages olabilir
            var fallbackUrl = $"/message/findMessages/{sessionName}";
            response = await _httpClient.PostAsJsonAsync(fallbackUrl, request);
            
            if (!response.IsSuccessStatusCode)
                return new List<EvolutionMessage>();
        }

        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return new List<EvolutionMessage>();

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        System.Text.Json.JsonElement arrayElement;
        
        // Doğrudan array ise (nadiren)
        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            arrayElement = root;
        }
        // Obje içinde "data" veya "messages" key'i altındaysa (v1 standart)
        else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                arrayElement = dataEl;
            else if (root.TryGetProperty("messages", out var msgEl) && msgEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                arrayElement = msgEl;
            else
                return new List<EvolutionMessage>();
        }
        else
        {
            return new List<EvolutionMessage>();
        }

        var messages = System.Text.Json.JsonSerializer.Deserialize<List<EvolutionMessage>>(arrayElement.GetRawText());
        return messages ?? new List<EvolutionMessage>();
    }
}
