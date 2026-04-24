using CafeBot.Business.DTOs;
using CafeBot.Data.Enums;
using CafeBot.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(IActivityLogRepository activityLogRepository, ILogger<ActivityController> logger)
    {
        _activityLogRepository = activityLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Aktivite loglarını sayfalı olarak döndürür.
    /// </summary>
    /// <param name="page">Sayfa numarası (1'den başlar)</param>
    /// <param name="pageSize">Sayfa başına kayıt sayısı (varsayılan: 50)</param>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(List<ActivityLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<ActivityLogDto>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1)
            return BadRequest("Sayfa numarası 1'den küçük olamaz.");

        if (pageSize < 1 || pageSize > 200)
            return BadRequest("Sayfa boyutu 1-200 arasında olmalıdır.");

        // Tüm logları al ve sayfalama uygula
        var allLogs = await _activityLogRepository.GetAllAsync();
        var pagedLogs = allLogs
            .OrderByDescending(log => log.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new ActivityLogDto(
                log.Id,
                log.Timestamp,
                log.Type,
                log.Message,
                log.Details))
            .ToList();

        return Ok(pagedLogs);
    }
}
