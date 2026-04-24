using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.Data.Repositories;

public class ConfigRepository : GenericRepository<Configuration>, IConfigRepository
{
    public ConfigRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Configuration?> GetConfigurationAsync()
    {
        // Configuration tablosunda tek kayıt olacak (Id = 1)
        return await _dbSet.FirstOrDefaultAsync(c => c.Id == 1);
    }
}
