namespace CafeBot.Data.Entities;

public class ProcessedMessage
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? SelectedHour { get; set; }
}
