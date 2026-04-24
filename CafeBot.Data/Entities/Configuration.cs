using CafeBot.Data.Enums;

namespace CafeBot.Data.Entities;

public class Configuration
{
    public int Id { get; set; }
    public string? SessionId { get; set; }              // Evolution API session ID
    public ConnectionStatus ConnectionStatus { get; set; }
    public string? TargetGroupId { get; set; }          // WhatsApp group ID
    public string? TargetGroupName { get; set; }
    public string PriorityListJson { get; set; } = "[]"; // JSON: [19, 20, 18]
    public SystemStatus SystemStatus { get; set; }
    public DateTime LastUpdated { get; set; }
}
