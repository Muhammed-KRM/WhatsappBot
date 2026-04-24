using CafeBot.Data.Entities;

namespace CafeBot.Data.Repositories;

public interface IProcessedMessageRepository : IRepository<ProcessedMessage>
{
    Task<ProcessedMessage?> GetByMessageIdAsync(string messageId);
    Task<bool> IsMessageProcessedAsync(string messageId);
}
