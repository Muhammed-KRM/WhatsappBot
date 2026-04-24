using System.Text;
using CafeBot.Business.Interfaces;

namespace CafeBot.API.Middleware;

/// <summary>
/// OZELDERS pattern'inden uyarlanan request/response logging middleware.
/// Her API isteğini hem console'a hem veritabanına (EndpointLog tablosu) kaydeder.
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    private static readonly string[] SkipPaths =
        ["/swagger", "/favicon", "/health", "/_blazor", "/hubs"];

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Swagger, health check, SignalR gibi path'leri atla
        if (SkipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Request body'yi oku
        context.Request.EnableBuffering();
        string? requestBody = null;
        if (context.Request.ContentLength > 0 && context.Request.ContentLength < 10_000)
        {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Response body'yi yakala
        var originalResponseBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            responseBuffer.Position = 0;
            var responseBody = await new StreamReader(responseBuffer).ReadToEndAsync();
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;

            // Console log
            var level = context.Response.StatusCode >= 500 ? LogLevel.Error
                      : context.Response.StatusCode >= 400 ? LogLevel.Warning
                      : LogLevel.Information;

            _logger.Log(level,
                "HTTP {Method} {Path}{Query} => {StatusCode} ({Duration}ms)",
                context.Request.Method,
                path,
                context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "",
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            // Veritabanı log - ayrı scope'da, ana akışı etkilemez
            var entry = new EndpointLogEntry
            {
                TraceId      = context.TraceIdentifier,
                Method       = context.Request.Method,
                Path         = path,
                Query        = context.Request.QueryString.Value,
                RequestBody  = requestBody,
                ResponseBody = responseBody,
                StatusCode   = context.Response.StatusCode,
                IpAddress    = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent    = context.Request.Headers.UserAgent.ToString(),
                DurationMs   = (int)stopwatch.ElapsedMilliseconds
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                    await logService.LogEndpointAsync(entry);
                }
                catch
                {
                    // Log hatası ana akışı etkilemesin
                }
            });
        }
    }
}
