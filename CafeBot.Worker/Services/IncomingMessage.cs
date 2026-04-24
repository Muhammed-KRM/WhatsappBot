namespace CafeBot.Worker.Services;

/// <summary>
/// Evolution API'den gelen ham WhatsApp mesajını temsil eder.
/// </summary>
public class IncomingMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
