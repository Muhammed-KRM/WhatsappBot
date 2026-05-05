using System.Text.Json;
using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using CafeBot.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace CafeBot.Business.Services;

public class ConfigManager : IConfigService
{
    private readonly IConfigRepository _configRepository;
    private readonly IGroupSettingsRepository _groupSettingsRepository;
    private readonly ILogger<ConfigManager> _logger;

    public ConfigManager(IConfigRepository configRepository, IGroupSettingsRepository groupSettingsRepository, ILogger<ConfigManager> logger)
    {
        _configRepository = configRepository;
        _groupSettingsRepository = groupSettingsRepository;
        _logger = logger;
    }

    public async Task<ConfigDto> GetConfigAsync()
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        try
        {
            _logger.LogInformation("🔍 [TRACE:{TraceId}] ConfigManager.GetConfigAsync BAŞLADI", traceId);
            
            var config = await _configRepository.GetConfigurationAsync();

            if (config == null)
            {
                _logger.LogWarning("⚠️ [TRACE:{TraceId}] Yapılandırma bulunamadı, varsayılan değerler döndürülüyor.", traceId);
                return new ConfigDto
                {
                    SessionId = null,
                    ConnectionStatus = ConnectionStatus.Disconnected,
                    TargetGroupIds = new List<string>(),
                    TargetGroupNames = new List<string>(),
                    PriorityList = new List<int>(),
                    SystemStatus = SystemStatus.Stopped,
                    LastUpdated = DateTime.UtcNow
                };
            }

            var dto = MapToDto(config);
            _logger.LogInformation("✅ [TRACE:{TraceId}] ConfigManager.GetConfigAsync TAMAMLANDI - SessionId: {SessionId}, ConnectionStatus: {Status}", 
                traceId, dto.SessionId ?? "NULL", dto.ConnectionStatus);
            
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TRACE:{TraceId}] ConfigManager.GetConfigAsync HATA", traceId);
            throw;
        }
    }

    public async Task UpdateTargetGroupsAsync(List<string> groupIds, List<string> groupNames)
    {
        try
        {
            var config = await GetOrCreateConfigurationAsync();

            config.TargetGroupIdsJson = JsonSerializer.Serialize(groupIds);
            config.TargetGroupNamesJson = JsonSerializer.Serialize(groupNames);
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Hedef gruplar güncellendi. Sayı: {Count}", groupIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hedef gruplar güncellenirken hata oluştu.");
            throw;
        }
    }

    public async Task UpdatePriorityListAsync(List<int> priorityList)
    {
        try
        {
            var config = await GetOrCreateConfigurationAsync();

            // Serialize priority list to JSON
            config.PriorityListJson = JsonSerializer.Serialize(priorityList);
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Öncelik listesi güncellendi. Liste: [{PriorityList}]", string.Join(", ", priorityList));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Öncelik listesi güncellenirken hata oluştu.");
            throw;
        }
    }

    public async Task UpdateSystemStatusAsync(SystemStatus status)
    {
        try
        {
            var config = await GetOrCreateConfigurationAsync();

            config.SystemStatus = status;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Sistem durumu güncellendi: {Status}", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sistem durumu güncellenirken hata oluştu. Status: {Status}", status);
            throw;
        }
    }

    public async Task UpdateConnectionStatusAsync(ConnectionStatus status)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        try
        {
            _logger.LogInformation("🔄 [TRACE:{TraceId}] ConfigManager.UpdateConnectionStatusAsync BAŞLADI - Yeni Status: {Status}", traceId, status);
            
            var config = await GetOrCreateConfigurationAsync();
            
            var oldStatus = config.ConnectionStatus;
            _logger.LogInformation("📝 [TRACE:{TraceId}] Eski Status: {OldStatus} → Yeni Status: {NewStatus}", 
                traceId, oldStatus, status);

            config.ConnectionStatus = status;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            
            _logger.LogInformation("✅ [TRACE:{TraceId}] ConnectionStatus DB'ye yazıldı: {Status}", traceId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TRACE:{TraceId}] ConfigManager.UpdateConnectionStatusAsync HATA - Status: {Status}", traceId, status);
            throw;
        }
    }

    public async Task UpdateSessionIdAsync(string? sessionId)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        try
        {
            _logger.LogInformation("🔑 [TRACE:{TraceId}] ConfigManager.UpdateSessionIdAsync BAŞLADI - Yeni SessionId: {SessionId}", traceId, sessionId ?? "NULL");
            
            var config = await GetOrCreateConfigurationAsync();
            
            var oldSessionId = config.SessionId;
            _logger.LogInformation("📝 [TRACE:{TraceId}] Eski SessionId: {OldSessionId} → Yeni SessionId: {NewSessionId}", 
                traceId, oldSessionId ?? "NULL", sessionId ?? "NULL");

            config.SessionId = sessionId;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            _logger.LogInformation("💾 [TRACE:{TraceId}] SaveChangesAsync çağrılıyor...", traceId);
            
            await _configRepository.SaveChangesAsync();
            
            _logger.LogInformation("✅ [TRACE:{TraceId}] SaveChangesAsync TAMAMLANDI - SessionId DB'ye yazıldı: {SessionId}", traceId, sessionId);
            
            // Doğrulama için hemen oku
            var verifyConfig = await _configRepository.GetConfigurationAsync();
            _logger.LogInformation("🔍 [TRACE:{TraceId}] DOĞRULAMA - DB'den okunan SessionId: {VerifySessionId}", 
                traceId, verifyConfig?.SessionId ?? "NULL");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [TRACE:{TraceId}] ConfigManager.UpdateSessionIdAsync HATA - SessionId: {SessionId}", traceId, sessionId);
            throw;
        }
    }

    private async Task<Configuration> GetOrCreateConfigurationAsync()
    {
        var config = await _configRepository.GetConfigurationAsync();

        if (config == null)
        {
            _logger.LogInformation("Yapılandırma bulunamadı, varsayılan yapılandırma oluşturuluyor.");
            // Create default configuration
            config = new Configuration
            {
                ConnectionStatus = ConnectionStatus.Disconnected,
                SystemStatus = SystemStatus.Stopped,
                PriorityListJson = "[]",
                TargetGroupIdsJson = "[]",
                TargetGroupNamesJson = "[]",
                LastUpdated = DateTime.UtcNow
            };

            config = await _configRepository.AddAsync(config);
            await _configRepository.SaveChangesAsync();
        }

        return config;
    }

    private ConfigDto MapToDto(Configuration config)
    {
        List<int> priorityList = new();
        List<string> targetGroupIds = new();
        List<string> targetGroupNames = new();
        
        try
        {
            priorityList = JsonSerializer.Deserialize<List<int>>(config.PriorityListJson) ?? new List<int>();
            targetGroupIds = JsonSerializer.Deserialize<List<string>>(config.TargetGroupIdsJson) ?? new List<string>();
            targetGroupNames = JsonSerializer.Deserialize<List<string>>(config.TargetGroupNamesJson) ?? new List<string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Konfigürasyon JSON ayrıştırılamadı, boş listeler kullanılıyor.");
        }

        return new ConfigDto
        {
            SessionId = config.SessionId,
            ConnectionStatus = config.ConnectionStatus,
            TargetGroupIds = targetGroupIds,
            TargetGroupNames = targetGroupNames,
            PriorityList = priorityList,
            AiSystemPrompt = config.AiSystemPrompt,
            SystemStatus = config.SystemStatus,
            LastUpdated = config.LastUpdated
        };
    }

    public async Task UpdateAiPromptAsync(string? prompt)
    {
        try
        {
            var config = await GetOrCreateConfigurationAsync();
            config.AiSystemPrompt = prompt;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Özel AI komutu başarıyla güncellendi.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Özel AI komutu güncellenirken hata oluştu");
            throw;
        }
    }

    public async Task<GroupSettingsDto> GetGroupSettingsAsync(string groupId)
    {
        var settings = await _groupSettingsRepository.GetByGroupIdAsync(groupId);
        if (settings == null)
        {
            // Default fallbacks from global config
            var globalConfig = await GetConfigAsync();
            return new GroupSettingsDto
            {
                GroupId = groupId,
                GroupName = "", // we don't have the name here easily, but UI should have it
                PriorityList = globalConfig.PriorityList,
                AiSystemPrompt = globalConfig.AiSystemPrompt
            };
        }

        List<int> priorityList = new();
        try
        {
            priorityList = JsonSerializer.Deserialize<List<int>>(settings.PriorityListJson) ?? new List<int>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "GroupSettings JSON ayrıştırılamadı.");
        }

        return new GroupSettingsDto
        {
            GroupId = settings.GroupId,
            GroupName = settings.GroupName,
            PriorityList = priorityList,
            AiSystemPrompt = settings.AiSystemPrompt
        };
    }

    public async Task UpdateGroupPriorityListAsync(string groupId, List<int> priorityList)
    {
        var settings = await _groupSettingsRepository.GetByGroupIdAsync(groupId);
        if (settings == null)
        {
            settings = new GroupSettings
            {
                GroupId = groupId,
                GroupName = "Unknown", // Will be updated if possible or leave it
                PriorityListJson = JsonSerializer.Serialize(priorityList),
                CreatedAt = DateTime.UtcNow
            };
            await _groupSettingsRepository.AddAsync(settings);
        }
        else
        {
            settings.PriorityListJson = JsonSerializer.Serialize(priorityList);
            _groupSettingsRepository.Update(settings);
        }
        await _groupSettingsRepository.SaveChangesAsync();
    }

    public async Task UpdateGroupAiPromptAsync(string groupId, string? prompt)
    {
        var settings = await _groupSettingsRepository.GetByGroupIdAsync(groupId);
        if (settings == null)
        {
            settings = new GroupSettings
            {
                GroupId = groupId,
                GroupName = "Unknown",
                PriorityListJson = "[]",
                AiSystemPrompt = prompt,
                CreatedAt = DateTime.UtcNow
            };
            await _groupSettingsRepository.AddAsync(settings);
        }
        else
        {
            settings.AiSystemPrompt = prompt;
            _groupSettingsRepository.Update(settings);
        }
        await _groupSettingsRepository.SaveChangesAsync();
    }
}
