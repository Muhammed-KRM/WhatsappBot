using System.Runtime.CompilerServices;
using System.Text.Json;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using Microsoft.Extensions.Logging;

namespace CafeBot.Business.Services;

/// <summary>
/// OZELDERS pattern'inden uyarlanan loglama servisi.
/// Endpoint request/response ve fonksiyon hatalarını SQLite veritabanına kaydeder.
/// </summary>
public class LogManager : ILogService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogManager> _logger;

    public LogManager(AppDbContext db, ILogger<LogManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogEndpointAsync(EndpointLogEntry entry)
    {
        try
        {
            var log = new EndpointLog
            {
                TraceId      = entry.TraceId,
                Method       = entry.Method,
                Path         = entry.Path,
                Query        = entry.Query,
                RequestBody  = MaskSensitiveData(entry.RequestBody),
                ResponseBody = entry.ResponseBody?.Length > 3000
                    ? entry.ResponseBody[..3000] + "...[truncated]"
                    : entry.ResponseBody,
                StatusCode   = entry.StatusCode,
                IpAddress    = entry.IpAddress,
                UserAgent    = entry.UserAgent,
                DurationMs   = entry.DurationMs,
                CreatedAt    = DateTime.UtcNow
            };

            _db.EndpointLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log yazma hatası uygulamayı çökertmemeli
            _logger.LogWarning(ex, "EndpointLog veritabanına yazılamadı.");
        }
    }

    public async Task LogFunctionErrorAsync(
        string errorCode,
        Exception ex,
        object? inputData = null,
        string? traceId = null,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            var className = Path.GetFileNameWithoutExtension(filePath);

            string? inputValue = null;
            if (inputData is not null)
            {
                try { inputValue = JsonSerializer.Serialize(inputData); }
                catch { inputValue = inputData.ToString(); }
            }

            var log = new FunctionLog
            {
                ErrorCode    = errorCode,
                ClassName    = className,
                MethodName   = memberName,
                FilePath     = filePath,
                LineNumber   = lineNumber,
                ErrorMessage = ex.Message,
                StackTrace   = ex.StackTrace,
                InputType    = inputData?.GetType().Name,
                InputValue   = inputValue,
                TraceId      = traceId,
                Severity     = ex is OutOfMemoryException or StackOverflowException ? "Critical" : "Error",
                CreatedAt    = DateTime.UtcNow
            };

            _db.FunctionLogs.Add(log);
            await _db.SaveChangesAsync();

            // Console'a da yaz
            _logger.LogError(ex,
                "[{ErrorCode}] {ClassName}.{MethodName}:{LineNumber} - {Message}",
                errorCode, className, memberName, lineNumber, ex.Message);
        }
        catch (Exception dbEx)
        {
            _logger.LogWarning(dbEx, "FunctionLog veritabanına yazılamadı.");
        }
    }

    public async Task LogProcessStepAsync(
        string traceId,
        int stepOrder,
        string stepName,
        string functionName,
        string? inputData = null,
        string? outputData = null,
        string status = "OK",
        string? errorMessage = null,
        int durationMs = 0)
    {
        try
        {
            var log = new ProcessLog
            {
                TraceId = traceId,
                StepOrder = stepOrder,
                StepName = stepName,
                FunctionName = functionName,
                InputData = inputData?.Length > 2000 ? inputData[..2000] + "...[truncated]" : inputData,
                OutputData = outputData?.Length > 2000 ? outputData[..2000] + "...[truncated]" : outputData,
                Status = status,
                ErrorMessage = errorMessage?.Length > 1000 ? errorMessage[..1000] + "..." : errorMessage,
                DurationMs = durationMs,
                CreatedAt = DateTime.UtcNow
            };

            _db.ProcessLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProcessLog veritabanına yazılamadı. Step: {StepName}", stepName);
        }
    }

    /// <summary>
    /// API key, şifre gibi hassas verileri maskeler.
    /// </summary>
    private static string? MaskSensitiveData(string? json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        return System.Text.RegularExpressions.Regex.Replace(
            json,
            @"""(apiKey|ApiKey|password|Password|token|Token|geminiKey|evolutionKey)""\s*:\s*""[^""]*""",
            @"""$1"":""***""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
