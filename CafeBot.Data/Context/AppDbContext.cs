using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CafeBot.Data.Interfaces;

namespace CafeBot.Data.Context;

public class AppDbContext : IdentityDbContext<AppUser>
{
    private readonly ICurrentUserService _currentUserService;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Configuration> Configurations { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<ProcessedMessage> ProcessedMessages { get; set; }
    public DbSet<EndpointLog> EndpointLogs { get; set; }
    public DbSet<FunctionLog> FunctionLogs { get; set; }
    public DbSet<ProcessLog> ProcessLogs { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<GroupSettings> GroupSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuration
        modelBuilder.Entity<Configuration>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ActivityLog - Timestamp üzerinde index
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_ActivityLog_Timestamp");
        });

        // ProcessedMessage - MessageId üzerinde unique constraint
        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.MessageId)
                .IsUnique()
                .HasDatabaseName("IX_ProcessedMessage_MessageId");
        });

        // EndpointLog - CreatedAt üzerinde index
        modelBuilder.Entity<EndpointLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_EndpointLog_CreatedAt");
            entity.HasIndex(e => e.StatusCode)
                .HasDatabaseName("IX_EndpointLog_StatusCode");
        });

        // FunctionLog - CreatedAt üzerinde index
        modelBuilder.Entity<FunctionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_FunctionLog_CreatedAt");
        });

        // ProcessLog - TraceId ve CreatedAt üzerinde index
        modelBuilder.Entity<ProcessLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TraceId)
                .HasDatabaseName("IX_ProcessLog_TraceId");
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_ProcessLog_CreatedAt");
        });

        // Global Query Filter for Multi-Tenant Isolation
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(
                    ConvertFilterExpression(entityType.ClrType));
            }
        }
    }

    private System.Linq.Expressions.LambdaExpression ConvertFilterExpression(Type entityType)
    {
        var newParam = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var property = System.Linq.Expressions.Expression.Property(newParam, nameof(ITenantEntity.UserId));
        
        var serviceInstance = System.Linq.Expressions.Expression.Constant(this);
        var serviceField = System.Linq.Expressions.Expression.Field(serviceInstance, "_currentUserService");
        var userIdProperty = System.Linq.Expressions.Expression.Property(serviceField, nameof(ICurrentUserService.UserId));

        var condition = System.Linq.Expressions.Expression.Equal(property, userIdProperty);
        return System.Linq.Expressions.Expression.Lambda(condition, newParam);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (string.IsNullOrEmpty(entry.Entity.UserId))
                {
                    if (string.IsNullOrEmpty(userId))
                    {
                        // Log entity'leri için anonim girişe izin ver (çökmemesi için)
                        if (entry.Entity is EndpointLog or FunctionLog or ProcessLog)
                        {
                            entry.Entity.UserId = "ANONYMOUS";
                        }
                        else
                        {
                            throw new InvalidOperationException($"Tenant UserId is required for {entry.Entity.GetType().Name} but no user is currently authenticated or provided.");
                        }
                    }
                    else
                    {
                        entry.Entity.UserId = userId;
                    }
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
