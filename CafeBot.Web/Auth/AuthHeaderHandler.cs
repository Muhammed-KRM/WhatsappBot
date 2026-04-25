using System.Net.Http.Headers;

namespace CafeBot.Web.Auth;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly ILogger<AuthHeaderHandler> _logger;
    private readonly TokenAuthenticationStateProvider _authStateProvider;

    public AuthHeaderHandler(TokenAuthenticationStateProvider authStateProvider, ILogger<AuthHeaderHandler> logger)
    {
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _authStateProvider.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogInformation("Bearer token added to request: {Path}", request.RequestUri?.PathAndQuery);
        }
        else
        {
            _logger.LogWarning("No bearer token found for request: {Path}", request.RequestUri?.PathAndQuery);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
