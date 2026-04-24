using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.Data.Repositories;

public class ActivityLogRepository : GenericRepository<ActivityLog>, IActivityLogRepository
{
    public ActivityLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ActivityLog>> GetRecentLogsAsync(int count = 50)
    {
        return await _dbSet
            .OrderByDescending(log => log.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivityLog>> GetLogsByTypeAsync(ActivityType type)
    {
        return await _dbSet
            .Where(log => log.Type == type)
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<ActivityLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(log => log.Timestamp >= startDate && log.Timestamp <= endDate)
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync();
    }
}
