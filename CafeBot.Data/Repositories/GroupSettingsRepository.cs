using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using CafeBot.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.Data.Repositories;

public class GroupSettingsRepository : GenericRepository<GroupSettings>, IGroupSettingsRepository
{
    public GroupSettingsRepository(AppDbContext context) 
        : base(context)
    {
    }

    public async Task<GroupSettings?> GetByGroupIdAsync(string groupId)
    {
        return await _dbSet.FirstOrDefaultAsync(g => g.GroupId == groupId);
    }
    
    public async Task<List<GroupSettings>> GetAllGroupsAsync()
    {
        return await _dbSet.ToListAsync();
    }
}
