using System.Security.Claims;
using CafeBot.Data.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CafeBot.Business.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _overrideUserId;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            if (!string.IsNullOrEmpty(_overrideUserId))
                return _overrideUserId;

            return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }

    public void SetCurrentUserId(string userId)
    {
        _overrideUserId = userId;
    }
}
