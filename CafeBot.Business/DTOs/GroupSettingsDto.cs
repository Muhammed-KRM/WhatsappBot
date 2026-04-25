namespace CafeBot.Business.DTOs;

public class GroupSettingsDto
{
    public string GroupId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public List<int> PriorityList { get; set; } = new();
    public string? AiSystemPrompt { get; set; }
}
