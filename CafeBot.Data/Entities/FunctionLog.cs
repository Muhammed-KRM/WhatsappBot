namespace CafeBot.Data.Entities;

/// <summary>
/// Fonksiyon hatalarını kaydeden log entity'si (OZELDERS pattern)
/// </summary>
public class FunctionLog
{
    public int Id { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public string? FilePath { get; set; }
    public int LineNumber { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? InputType { get; set; }
    public string? InputValue { get; set; }
    public string? TraceId { get; set; }
    public string Severity { get; set; } = "Error"; // Error, Critical
    public DateTime CreatedAt { get; set; }
}
