using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<WhatsAppController> _logger;

    public WhatsAppController(IWhatsAppService whatsAppService, ILogger<WhatsAppController> logger)
    {
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    /// <summary>
    /// WhatsApp oturumu başlatır ve QR kod döndürür.
    /// </summary>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(QRCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QRCodeDto>> Connect()
    {
        _logger.LogInformation("WhatsApp bağlantısı başlatılıyor.");
        var qrCode = await _whatsAppService.InitializeSessionAsync();
        return Ok(qrCode);
    }

    /// <summary>
    /// Mevcut WhatsApp bağlantı durumunu döndürür.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ConnectionStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConnectionStatus>> GetStatus()
    {
        var status = await _whatsAppService.GetConnectionStatusAsync();
        return Ok(status);
    }

    /// <summary>
    /// Bağlı oturumdaki tüm WhatsApp gruplarını listeler.
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(List<GroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<GroupDto>>> GetGroups()
    {
        var groups = await _whatsAppService.GetGroupsAsync();
        return Ok(groups);
    }

    /// <summary>
    /// WhatsApp oturumunu sonlandırır.
    /// </summary>
    [HttpPost("disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect()
    {
        _logger.LogInformation("WhatsApp bağlantısı kesiliyor.");
        await _whatsAppService.DisconnectAsync();
        return NoContent();
    }
}
