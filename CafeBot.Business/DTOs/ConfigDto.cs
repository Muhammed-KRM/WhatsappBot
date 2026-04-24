using CafeBot.Data.Enums;

namespace CafeBot.Business.DTOs;

public record ConfigDto
{
    public string? SessionId { get; init; }
    public ConnectionStatus ConnectionStatus { get; init; }
    public string? TargetGroupId { get; init; }
    public string? TargetGroupName { get; init; }
    public List<int> PriorityList { get; init; } = new();
    public SystemStatus SystemStatus { get; init; }
    public DateTime LastUpdated { get; init; }
}
