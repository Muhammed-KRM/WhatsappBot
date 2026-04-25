namespace CafeBot.Data.Entities;

/// <summary>
/// Her seçilen grup için ayrı ayarlar (öncelik listesi, AI prompt vb.)
/// </summary>
public class GroupSettings : ITenantEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;  // WhatsApp Group JID
    public string GroupName { get; set; } = string.Empty;
    public string PriorityListJson { get; set; } = "[]";
    public string? AiSystemPrompt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
