using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogController> _logger;

    public LogController(AppDbContext db, ILogger<LogController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint loglarını sayfalı döndürür (her API isteği)
    /// </summary>
    [HttpGet("endpoints")]
    public async Task<ActionResult<EndpointLogsResponse>> GetEndpointLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? statusCode = null)
    {
        var query = _db.EndpointLogs.AsQueryable();

        if (statusCode.HasValue)
            query = query.Where(l => l.StatusCode == statusCode.Value);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new EndpointLogDto
            {
                Id         = l.Id,
                TraceId    = l.TraceId,
                Method     = l.Method,
                Path       = l.Path,
                Query      = l.Query,
                RequestBody = l.RequestBody,
                ResponseBody = l.ResponseBody,
                StatusCode = l.StatusCode,
                DurationMs = l.DurationMs,
                IpAddress  = l.IpAddress,
                CreatedAt  = l.CreatedAt
            })
            .ToListAsync();

        return Ok(new EndpointLogsResponse { Total = total, Page = page, PageSize = pageSize, Logs = logs });
    }

    /// <summary>
    /// Endpoint log detayını döndürür (request/response body dahil)
    /// </summary>
    [HttpGet("endpoints/{id}")]
    public async Task<ActionResult<EndpointLog>> GetEndpointLogDetail(int id)
    {
        var log = await _db.EndpointLogs.FindAsync(id);
        if (log == null) return NotFound();
        return Ok(log);
    }

    /// <summary>
    /// Fonksiyon hata loglarını sayfalı döndürür
    /// </summary>
    [HttpGet("functions")]
    public async Task<ActionResult<FunctionLogsResponse>> GetFunctionLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var total = await _db.FunctionLogs.CountAsync();
        var logs = await _db.FunctionLogs
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new FunctionLogsResponse { Total = total, Page = page, PageSize = pageSize, Logs = logs });
    }

    /// <summary>
    /// Log istatistiklerini döndürür
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<LogStatsDto>> GetStats()
    {
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);

        var stats = new LogStatsDto
        {
            TotalEndpointLogs  = await _db.EndpointLogs.CountAsync(),
            TotalFunctionErrors = await _db.FunctionLogs.CountAsync(),
            Last24hRequests    = await _db.EndpointLogs.CountAsync(l => l.CreatedAt >= last24h),
            Last24hErrors      = await _db.EndpointLogs.CountAsync(l => l.CreatedAt >= last24h && l.StatusCode >= 500),
            Last24hFunctionErrors = await _db.FunctionLogs.CountAsync(l => l.CreatedAt >= last24h),
            AvgDurationMs      = await _db.EndpointLogs.AnyAsync()
                ? (int)await _db.EndpointLogs.AverageAsync(l => (double)l.DurationMs)
                : 0
        };

        return Ok(stats);
    }

    /// <summary>
    /// Süreç loglarını sayfalı döndürür (mesaj bazlı gruplu)
    /// Her trace = bir mesaj işleme süreci
    /// </summary>
    [HttpGet("process")]
    public async Task<ActionResult<ProcessLogsResponse>> GetProcessLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Her trace'in özet bilgisini al (ilk ve son adım)
        var traceIds = await _db.ProcessLogs
            .GroupBy(p => p.TraceId)
            .Select(g => new ProcessTraceSummaryDto
            {
                TraceId = g.Key,
                TotalSteps = g.Count(),
                StartTime = g.Min(p => p.CreatedAt),
                EndTime = g.Max(p => p.CreatedAt),
                TotalDurationMs = g.Sum(p => p.DurationMs),
                HasError = g.Any(p => p.Status == "Error"),
                LastStepName = g.OrderByDescending(p => p.StepOrder).First().StepName,
                LastStepStatus = g.OrderByDescending(p => p.StepOrder).First().Status,
                ErrorMessage = g.Where(p => p.Status == "Error").OrderByDescending(p => p.StepOrder).First().ErrorMessage
            })
            .OrderByDescending(t => t.StartTime)
            .ToListAsync();

        var total = traceIds.Count;
        var paged = traceIds
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new ProcessLogsResponse { Total = total, Page = page, PageSize = pageSize, Traces = paged });
    }

    /// <summary>
    /// Belirli bir trace'in tüm adımlarını döndürür (timeline)
    /// </summary>
    [HttpGet("process/{traceId}")]
    public async Task<ActionResult<List<ProcessLogDto>>> GetProcessTrace(string traceId)
    {
        var steps = await _db.ProcessLogs
            .Where(p => p.TraceId == traceId)
            .OrderBy(p => p.StepOrder)
            .Select(p => new ProcessLogDto
            {
                Id = p.Id,
                TraceId = p.TraceId,
                StepOrder = p.StepOrder,
                StepName = p.StepName,
                FunctionName = p.FunctionName,
                InputData = p.InputData,
                OutputData = p.OutputData,
                Status = p.Status,
                ErrorMessage = p.ErrorMessage,
                DurationMs = p.DurationMs,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        if (steps.Count == 0) return NotFound();
        return Ok(steps);
    }
}

// DTOs
public class EndpointLogDto
{
    public int Id { get; set; }
    public string? TraceId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Query { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EndpointLogsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<EndpointLogDto> Logs { get; set; } = new();
}

public class FunctionLogsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<FunctionLog> Logs { get; set; } = new();
}

public class LogStatsDto
{
    public int TotalEndpointLogs { get; set; }
    public int TotalFunctionErrors { get; set; }
    public int Last24hRequests { get; set; }
    public int Last24hErrors { get; set; }
    public int Last24hFunctionErrors { get; set; }
    public int AvgDurationMs { get; set; }
}

// Process Log DTOs
public class ProcessLogDto
{
    public int Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public string Status { get; set; } = "OK";
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProcessTraceSummaryDto
{
    public string TraceId { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalDurationMs { get; set; }
    public bool HasError { get; set; }
    public string LastStepName { get; set; } = string.Empty;
    public string LastStepStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class ProcessLogsResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ProcessTraceSummaryDto> Traces { get; set; } = new();
}
