using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;

namespace CafeBot.Business.Interfaces;

public interface IConfigService
{
    Task<ConfigDto> GetConfigAsync();
    Task UpdateTargetGroupsAsync(List<string> groupIds, List<string> groupNames);
    Task UpdatePriorityListAsync(List<int> priorityList);
    Task UpdateSystemStatusAsync(SystemStatus status);
    Task UpdateConnectionStatusAsync(ConnectionStatus status);
    Task UpdateSessionIdAsync(string? sessionId);
    Task UpdateAiPromptAsync(string? prompt);
    Task<GroupSettingsDto> GetGroupSettingsAsync(string groupId);
    Task UpdateGroupPriorityListAsync(string groupId, List<int> priorityList);
    Task UpdateGroupAiPromptAsync(string groupId, string? prompt);
}
