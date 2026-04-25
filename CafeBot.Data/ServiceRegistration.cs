using CafeBot.Data.Context;
using CafeBot.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CafeBot.Data;

public static class ServiceRegistration
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, string connectionString)
    {
        // DbContext kaydı (SQLite)
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // Repository kayıtları (Scoped)
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IConfigRepository, ConfigRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();
        services.AddScoped<IGroupSettingsRepository, GroupSettingsRepository>();

        return services;
    }
}
