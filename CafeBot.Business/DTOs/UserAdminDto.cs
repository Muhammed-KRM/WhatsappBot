namespace CafeBot.Business.DTOs;

public class UserAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ConnectionStatus { get; set; } = string.Empty;
    public string SystemStatus { get; set; } = string.Empty;
    public int SelectedGroupCount { get; set; }
    public bool IsBanned { get; set; }
}
