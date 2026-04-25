namespace CafeBot.Data.Entities;

/// <summary>
/// Her API endpoint isteğini kaydeden log entity'si (OZELDERS pattern)
/// </summary>
public class EndpointLog : ITenantEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
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
    public DateTime CreatedAt { get; set; }
}
