namespace CafeBot.Business.DTOs;

public class LoginRequestDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class RegisterRequestDto
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? CompanyName { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
}
