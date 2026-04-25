using CafeBot.Data.Enums;

namespace CafeBot.Business.DTOs;

public record ConfigDto
{
    public string? SessionId { get; init; }
    public ConnectionStatus ConnectionStatus { get; init; }
    public List<string> TargetGroupIds { get; init; } = new();
    public List<string> TargetGroupNames { get; init; } = new();
    public List<int> PriorityList { get; init; } = new();
    public string? AiSystemPrompt { get; init; }
    public SystemStatus SystemStatus { get; init; }
    public DateTime LastUpdated { get; init; }
}
