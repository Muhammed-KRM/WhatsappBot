using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Entities;
using CafeBot.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly IConfigService _configService;
    private readonly ILogger<ConfigController> _logger;
    private readonly UserManager<AppUser> _userManager;

    public ConfigController(IConfigService configService, ILogger<ConfigController> logger, UserManager<AppUser> userManager)
    {
        _configService = configService;
        _logger = logger;
        _userManager = userManager;
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
    public async Task<IActionResult> UpdateGroup([FromBody] UpdateGroupsRequest request)
    {
        if (request.GroupIds == null || request.GroupNames == null)
            return BadRequest("Grup bilgileri boş olamaz.");

        if (request.GroupIds.Count != request.GroupNames.Count)
            return BadRequest("Grup ID ve İsim listeleri aynı uzunlukta olmalıdır.");

        _logger.LogInformation("Hedef gruplar güncelleniyor: {Count} grup", request.GroupIds.Count);
        await _configService.UpdateTargetGroupsAsync(request.GroupIds, request.GroupNames);
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

        if (status == SystemStatus.Running)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null && user.IsBanned)
                {
                    return BadRequest("Hesabınız yasaklanmıştır. Sistemi başlatamazsınız.");
                }
            }
        }

        _logger.LogInformation("Sistem durumu güncelleniyor: {Status}", status);
        await _configService.UpdateSystemStatusAsync(status);
        return NoContent();
    }
    /// <summary>
    /// Özel Yapay Zeka komutunu günceller.
    /// </summary>
    [HttpPut("ai-prompt")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAiPrompt([FromBody] UpdateAiPromptRequest request)
    {
        _logger.LogInformation("Özel AI komutu güncelleniyor.");
        await _configService.UpdateAiPromptAsync(request.Prompt);
        return NoContent();
    }

    [HttpGet("group-settings/{groupId}")]
    public async Task<ActionResult<GroupSettingsDto>> GetGroupSettings(string groupId)
    {
        var settings = await _configService.GetGroupSettingsAsync(groupId);
        return Ok(settings);
    }

    [HttpPut("group-settings/{groupId}/priority")]
    public async Task<IActionResult> UpdateGroupPriority(string groupId, [FromBody] List<int> priorityList)
    {
        if (priorityList == null || priorityList.Count == 0)
            return BadRequest("Öncelik listesi en az bir saat değeri içermelidir.");
        if (priorityList.Distinct().Count() != priorityList.Count)
            return BadRequest("Öncelik listesinde tekrar eden saat değerleri olamaz.");
        if (priorityList.Any(h => h < 0 || h > 23))
            return BadRequest("Saat değerleri 0-23 arasında olmalıdır.");

        await _configService.UpdateGroupPriorityListAsync(groupId, priorityList);
        return NoContent();
    }

    [HttpPut("group-settings/{groupId}/ai-prompt")]
    public async Task<IActionResult> UpdateGroupAiPrompt(string groupId, [FromBody] UpdateAiPromptRequest request)
    {
        await _configService.UpdateGroupAiPromptAsync(groupId, request.Prompt);
        return NoContent();
    }
}

/// <summary>
/// AI Komut güncelleme isteği modeli.
/// </summary>
public record UpdateAiPromptRequest(string? Prompt);

/// <summary>
/// Grup güncelleme isteği modeli.
/// </summary>
public record UpdateGroupsRequest(List<string> GroupIds, List<string> GroupNames);
