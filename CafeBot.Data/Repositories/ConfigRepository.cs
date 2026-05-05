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
        // AsNoTracking: DbContext cache'ini bypass et, her seferinde DB'den taze veri oku
        return await _dbSet.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1);
    }
}
