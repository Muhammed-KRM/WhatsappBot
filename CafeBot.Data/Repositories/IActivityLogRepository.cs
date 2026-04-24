using CafeBot.Data.Entities;
using CafeBot.Data.Enums;

namespace CafeBot.Data.Repositories;

public interface IActivityLogRepository : IRepository<ActivityLog>
{
    Task<IEnumerable<ActivityLog>> GetRecentLogsAsync(int count = 50);
    Task<IEnumerable<ActivityLog>> GetLogsByTypeAsync(ActivityType type);
    Task<IEnumerable<ActivityLog>> GetLogsByDateRangeAsync(DateTime startDate, DateTime endDate);
}
