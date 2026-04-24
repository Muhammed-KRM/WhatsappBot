using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;

namespace CafeBot.Business.Interfaces;

public interface IConfigService
{
    Task<ConfigDto> GetConfigAsync();
    Task UpdateTargetGroupAsync(string groupId, string groupName);
    Task UpdatePriorityListAsync(List<int> priorityList);
    Task UpdateSystemStatusAsync(SystemStatus status);
    Task UpdateConnectionStatusAsync(ConnectionStatus status);
    Task UpdateSessionIdAsync(string? sessionId);
}
