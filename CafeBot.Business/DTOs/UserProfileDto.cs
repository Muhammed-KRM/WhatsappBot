namespace CafeBot.Business.DTOs;

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? PersonalGeminiApiKey { get; set; }
}

public class UpdateUserProfileDto
{
    public string? CompanyName { get; set; }
    public string? PersonalGeminiApiKey { get; set; }
}
