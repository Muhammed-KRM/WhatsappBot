using CafeBot.Data.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace CafeBot.API.Middleware;

/// <summary>
/// Middleware to ensure users have a valid API key before accessing protected endpoints
/// </summary>
public class ApiKeyRequiredMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyRequiredMiddleware> _logger;

    // Endpoints that don't require API key validation
    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/register", 
        "/api/user/profile",
        "/api/user/validate-api-key",
        "/api/user/has-valid-api-key",
        "/hubs/activity",
        "/favicon.ico",
        "/health"
    };

    public ApiKeyRequiredMiddleware(RequestDelegate next, ILogger<ApiKeyRequiredMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<AppUser> userManager)
    {
        // Skip middleware for exempt paths
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Skip middleware for non-API requests (static files, etc.)
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Skip middleware for unauthenticated requests (will be handled by auth middleware)
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Check if user is admin (admins can use system API key)
        if (context.User.IsInRole("Admin"))
        {
            await _next(context);
            return;
        }

        // Get current user and check if they have a valid API key
        var user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Kullanıcı bulunamadı.");
            return;
        }

        // Check if user has personal API key
        if (string.IsNullOrWhiteSpace(user.PersonalGeminiApiKey))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "API_KEY_REQUIRED",
                message = "Bu işlemi gerçekleştirmek için kişisel Gemini API anahtarınızı girmeniz gerekiyor.",
                redirectTo = "/profile"
            }));
            return;
        }

        await _next(context);
    }

    private static bool IsExemptPath(PathString path)
    {
        return ExemptPaths.Any(exemptPath => 
            path.StartsWithSegments(exemptPath, StringComparison.OrdinalIgnoreCase));
    }
}