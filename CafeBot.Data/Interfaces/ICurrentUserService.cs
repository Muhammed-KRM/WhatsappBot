namespace CafeBot.Data.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    void SetCurrentUserId(string userId);
}
