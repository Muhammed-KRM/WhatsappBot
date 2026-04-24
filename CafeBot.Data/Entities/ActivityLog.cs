using CafeBot.Data.Enums;

namespace CafeBot.Data.Entities;

public class ActivityLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public ActivityType Type { get; set; }              // Info, Success, Error, Warning
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }                // JSON details
}
