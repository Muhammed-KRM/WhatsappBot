using CafeBot.Data.Enums;

namespace CafeBot.Data.Entities;

public class Configuration : ITenantEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? SessionId { get; set; }              // Evolution API session ID
    public ConnectionStatus ConnectionStatus { get; set; }
    public string TargetGroupIdsJson { get; set; } = "[]";          // JSON array of group IDs
    public string TargetGroupNamesJson { get; set; } = "[]";        // JSON array of group names
    public string PriorityListJson { get; set; } = "[]"; // JSON: [19, 20, 18]
    public string? AiSystemPrompt { get; set; }          // Özel Gemini Yapay Zeka Komutu
    public SystemStatus SystemStatus { get; set; }
    public DateTime LastUpdated { get; set; }
}
