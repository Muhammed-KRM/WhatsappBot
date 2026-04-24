using CafeBot.Data.Entities;

namespace CafeBot.Data.Repositories;

public interface IConfigRepository : IRepository<Configuration>
{
    Task<Configuration?> GetConfigurationAsync();
}
