using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.Data.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Configuration> Configurations { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<ProcessedMessage> ProcessedMessages { get; set; }
    public DbSet<EndpointLog> EndpointLogs { get; set; }
    public DbSet<FunctionLog> FunctionLogs { get; set; }
    public DbSet<ProcessLog> ProcessLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuration - tek kayıt olacak, seed data ile başlatılır
        modelBuilder.Entity<Configuration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasData(new Configuration
            {
                Id = 1,
                ConnectionStatus = ConnectionStatus.Disconnected,
                SystemStatus = SystemStatus.Stopped,
                PriorityListJson = "[]",
                LastUpdated = DateTime.UtcNow
            });
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
    }
}
