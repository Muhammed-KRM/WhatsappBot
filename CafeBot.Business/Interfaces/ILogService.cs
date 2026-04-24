using System.Runtime.CompilerServices;

namespace CafeBot.Business.Interfaces;

/// <summary>
/// OZELDERS pattern'inden uyarlanan loglama servisi interface'i.
/// Hem endpoint request/response hem de fonksiyon hatalarını veritabanına kaydeder.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// API endpoint isteğini veritabanına kaydeder.
    /// </summary>
    Task LogEndpointAsync(EndpointLogEntry entry);

    /// <summary>
    /// Fonksiyon hatasını veritabanına kaydeder.
    /// CallerMemberName, CallerFilePath, CallerLineNumber otomatik doldurulur.
    /// </summary>
    Task LogFunctionErrorAsync(
        string errorCode,
        Exception ex,
        object? inputData = null,
        string? traceId = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0);

    /// <summary>
    /// Süreç adımını ProcessLog tablosuna kaydeder.
    /// Breakpoint mantığı: her fonksiyonda ne girdi, ne çıktı loglanır.
    /// </summary>
    Task LogProcessStepAsync(
        string traceId,
        int stepOrder,
        string stepName,
        string functionName,
        string? inputData = null,
        string? outputData = null,
        string status = "OK",
        string? errorMessage = null,
        int durationMs = 0);
}

public class EndpointLogEntry
{
    public string? TraceId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Query { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int DurationMs { get; set; }
}
