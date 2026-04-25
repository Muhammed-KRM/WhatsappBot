using CafeBot.Data.Entities;

namespace CafeBot.Data.Repositories;

public interface IGroupSettingsRepository : IRepository<GroupSettings>
{
    Task<GroupSettings?> GetByGroupIdAsync(string groupId);
    Task<List<GroupSettings>> GetAllGroupsAsync();
}
