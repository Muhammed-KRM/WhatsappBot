namespace CafeBot.Data.Entities;

public class ProcessedMessage : ITenantEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? SelectedHour { get; set; }
}
