using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.Data.Repositories;

public class ProcessedMessageRepository : GenericRepository<ProcessedMessage>, IProcessedMessageRepository
{
    public ProcessedMessageRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ProcessedMessage?> GetByMessageIdAsync(string messageId)
    {
        return await _dbSet.FirstOrDefaultAsync(pm => pm.MessageId == messageId);
    }

    public async Task<bool> IsMessageProcessedAsync(string messageId)
    {
        return await _dbSet.AnyAsync(pm => pm.MessageId == messageId);
    }
}
