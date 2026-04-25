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
        try
        {
            var config = await _configRepository.GetConfigurationAsync();

            if (config == null)
            {
                _logger.LogDebug("Yapılandırma bulunamadı, varsayılan değerler döndürülüyor.");
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

            return MapToDto(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yapılandırma alınırken hata oluştu.");
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
        try
        {
            var config = await GetOrCreateConfigurationAsync();

            config.ConnectionStatus = status;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Bağlantı durumu güncellendi: {Status}", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bağlantı durumu güncellenirken hata oluştu. Status: {Status}", status);
            throw;
        }
    }

    public async Task UpdateSessionIdAsync(string? sessionId)
    {
        try
        {
            var config = await GetOrCreateConfigurationAsync();

            config.SessionId = sessionId;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Oturum ID güncellendi: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Oturum ID güncellenirken hata oluştu. SessionId: {SessionId}", sessionId);
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
