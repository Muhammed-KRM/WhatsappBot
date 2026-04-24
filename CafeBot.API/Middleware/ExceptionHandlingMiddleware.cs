using System.Net;
using System.Text.Json;
using CafeBot.Business.Interfaces;

namespace CafeBot.API.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Tüm işlenmemiş hataları yakalar, hem console'a hem FunctionLog tablosuna kaydeder.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            ArgumentNullException    => (HttpStatusCode.BadRequest, "Gerekli parametre eksik."),
            ArgumentException        => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException     => (HttpStatusCode.NotFound, "İstenen kaynak bulunamadı."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Bu işlem için yetkiniz yok."),
            HttpRequestException     => (HttpStatusCode.BadGateway, "Harici servis bağlantı hatası."),
            TaskCanceledException    => (HttpStatusCode.RequestTimeout, "İstek zaman aşımına uğradı."),
            _                        => (HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu.")
        };

        // Console log
        _logger.LogError(
            exception,
            "İşlenmemiş hata. StatusCode: {StatusCode}, Path: {Path}, Method: {Method}",
            (int)statusCode,
            context.Request.Path,
            context.Request.Method);

        // Veritabanı log - FunctionLog tablosuna yaz
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
                await logService.LogFunctionErrorAsync(
                    errorCode: $"HTTP_{(int)statusCode}",
                    ex: exception,
                    inputData: new { Path = context.Request.Path.Value, Method = context.Request.Method },
                    traceId: context.TraceIdentifier);
            }
            catch
            {
                // Log hatası ana akışı etkilemesin
            }
        });

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ErrorResponse(
            StatusCode: (int)statusCode,
            Message: message,
            Detail: null // Production'da stack trace gösterme
        );

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}

public record ErrorResponse(int StatusCode, string Message, string? Detail);
