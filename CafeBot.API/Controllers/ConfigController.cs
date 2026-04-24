using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IConfigService configService, ILogger<ConfigController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Mevcut sistem yapılandırmasını döndürür.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfigDto>> GetConfig()
    {
        var config = await _configService.GetConfigAsync();
        return Ok(config);
    }

    /// <summary>
    /// Hedef WhatsApp grubunu günceller.
    /// </summary>
    [HttpPut("group")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateGroup([FromBody] UpdateGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GroupId))
            return BadRequest("Grup ID boş olamaz.");

        if (string.IsNullOrWhiteSpace(request.GroupName))
            return BadRequest("Grup adı boş olamaz.");

        _logger.LogInformation("Hedef grup güncelleniyor: {GroupId} - {GroupName}", request.GroupId, request.GroupName);
        await _configService.UpdateTargetGroupAsync(request.GroupId, request.GroupName);
        return NoContent();
    }

    /// <summary>
    /// Öncelik listesini günceller.
    /// </summary>
    [HttpPut("priority")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePriority([FromBody] List<int> priorityList)
    {
        if (priorityList == null || priorityList.Count == 0)
            return BadRequest("Öncelik listesi en az bir saat değeri içermelidir.");

        if (priorityList.Distinct().Count() != priorityList.Count)
            return BadRequest("Öncelik listesinde tekrar eden saat değerleri olamaz.");

        if (priorityList.Any(h => h < 0 || h > 23))
            return BadRequest("Saat değerleri 0-23 arasında olmalıdır.");

        _logger.LogInformation("Öncelik listesi güncelleniyor: [{PriorityList}]", string.Join(", ", priorityList));
        await _configService.UpdatePriorityListAsync(priorityList);
        return NoContent();
    }

    /// <summary>
    /// Sistem durumunu günceller (Başlat/Durdur).
    /// </summary>
    [HttpPut("system-status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSystemStatus([FromBody] SystemStatus status)
    {
        if (!Enum.IsDefined(typeof(SystemStatus), status))
            return BadRequest("Geçersiz sistem durumu.");

        _logger.LogInformation("Sistem durumu güncelleniyor: {Status}", status);
        await _configService.UpdateSystemStatusAsync(status);
        return NoContent();
    }
}

/// <summary>
/// Grup güncelleme isteği modeli.
/// </summary>
public record UpdateGroupRequest(string GroupId, string GroupName);
