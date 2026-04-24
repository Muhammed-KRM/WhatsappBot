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
    private readonly ILogger<ConfigManager> _logger;

    public ConfigManager(IConfigRepository configRepository, ILogger<ConfigManager> logger)
    {
        _configRepository = configRepository;
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
                    TargetGroupId = null,
                    TargetGroupName = null,
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

    public async Task UpdateTargetGroupAsync(string groupId, string groupName)
    {
        try
        {
            var config = await GetOrCreateConfigurationAsync();

            config.TargetGroupId = groupId;
            config.TargetGroupName = groupName;
            config.LastUpdated = DateTime.UtcNow;

            _configRepository.Update(config);
            await _configRepository.SaveChangesAsync();
            _logger.LogInformation("Hedef grup güncellendi. GroupId: {GroupId}, GroupName: {GroupName}", groupId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hedef grup güncellenirken hata oluştu. GroupId: {GroupId}", groupId);
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
                LastUpdated = DateTime.UtcNow
            };

            config = await _configRepository.AddAsync(config);
            await _configRepository.SaveChangesAsync();
        }

        return config;
    }

    private ConfigDto MapToDto(Configuration config)
    {
        // Deserialize priority list from JSON
        List<int> priorityList;
        try
        {
            priorityList = JsonSerializer.Deserialize<List<int>>(config.PriorityListJson) ?? new List<int>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Öncelik listesi JSON'dan ayrıştırılamadı, boş liste kullanılıyor.");
            priorityList = new List<int>();
        }

        return new ConfigDto
        {
            SessionId = config.SessionId,
            ConnectionStatus = config.ConnectionStatus,
            TargetGroupId = config.TargetGroupId,
            TargetGroupName = config.TargetGroupName,
            PriorityList = priorityList,
            SystemStatus = config.SystemStatus,
            LastUpdated = config.LastUpdated
        };
    }
}
