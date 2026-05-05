using CafeBot.Business.DTOs;
using CafeBot.Business.Interfaces;
using CafeBot.Data.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<WhatsAppController> _logger;
    private readonly ILogService _logService;

    public WhatsAppController(IWhatsAppService whatsAppService, ILogger<WhatsAppController> logger, ILogService logService)
    {
        _whatsAppService = whatsAppService;
        _logger = logger;
        _logService = logService;
    }

    /// <summary>
    /// WhatsApp oturumu başlatır ve QR kod döndürür.
    /// </summary>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(QRCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QRCodeDto>> Connect()
    {
        var traceId = $"conn-{Guid.NewGuid().ToString("N")[..8]}";
        var stepOrder = 0;

        try
        {
            _logger.LogInformation("🚀 [TRACE:{TraceId}] WhatsAppController.Connect() BAŞLADI", traceId);
            
            // Step 1: Controller giriş
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "Controller.Connect Giriş", "WhatsAppController.Connect",
                inputData: $"{{\"user\":\"{User.Identity?.Name}\"}}",
                outputData: null, status: "OK");

            // Step 2: InitializeSessionAsync çağrısı
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "InitializeSession Çağrısı", "WhatsAppService.InitializeSessionAsync",
                inputData: null, outputData: null, status: "BAŞLADI");
            
            var qrCode = await _whatsAppService.InitializeSessionAsync(traceId);
            
            // Step 3: QR Kod sonucu
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "QR Kod Alındı", "WhatsAppController.Connect",
                inputData: null,
                outputData: $"{{\"sessionName\":\"{qrCode.SessionName}\",\"qrBase64Length\":{qrCode.Base64Image?.Length ?? 0}}}",
                status: "OK");

            _logger.LogInformation("✅ [TRACE:{TraceId}] WhatsAppController.Connect() TAMAMLANDI - QR döndürülüyor", traceId);
            return Ok(qrCode);
        }
        catch (Exception ex)
        {
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "Controller.Connect HATA", "WhatsAppController.Connect",
                inputData: null, outputData: null, status: "Error",
                errorMessage: $"{ex.GetType().Name}: {ex.Message}");
            
            _logger.LogError(ex, "❌ [TRACE:{TraceId}] WhatsAppController.Connect() HATA", traceId);
            return StatusCode(500, new { error = ex.Message, traceId });
        }
    }

    /// <summary>
    /// Mevcut WhatsApp bağlantı durumunu döndürür.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ConnectionStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConnectionStatus>> GetStatus()
    {
        var traceId = $"stat-{Guid.NewGuid().ToString("N")[..8]}";
        var stepOrder = 0;

        try
        {
            // Step 1: Status sorgusu başladı
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "Controller.GetStatus Giriş", "WhatsAppController.GetStatus",
                inputData: $"{{\"user\":\"{User.Identity?.Name}\"}}",
                outputData: null, status: "OK");

            var status = await _whatsAppService.GetConnectionStatusAsync(traceId);
            
            // Step 2: Sonuç
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "Status Sonucu", "WhatsAppController.GetStatus",
                inputData: null,
                outputData: $"{{\"status\":\"{status}\"}}",
                status: "OK");

            return Ok(status);
        }
        catch (Exception ex)
        {
            stepOrder++;
            await _logService.LogProcessStepAsync(traceId, stepOrder, "Controller.GetStatus HATA", "WhatsAppController.GetStatus",
                inputData: null, outputData: null, status: "Error",
                errorMessage: ex.Message);
            
            return Ok(ConnectionStatus.Error);
        }
    }

    /// <summary>
    /// Bağlı oturumdaki tüm WhatsApp gruplarını listeler.
    /// </summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(List<GroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<GroupDto>>> GetGroups([FromQuery] bool forceRefresh = false)
    {
        var groups = await _whatsAppService.GetGroupsAsync(forceRefresh);
        return Ok(groups);
    }

    /// <summary>
    /// WhatsApp oturumunu sonlandırır.
    /// </summary>
    [HttpPost("disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect()
    {
        var traceId = $"disc-{Guid.NewGuid().ToString("N")[..8]}";
        
        _logger.LogInformation("🔌 [TRACE:{TraceId}] WhatsApp bağlantısı kesiliyor.", traceId);
        await _logService.LogProcessStepAsync(traceId, 1, "Disconnect Başladı", "WhatsAppController.Disconnect",
            inputData: $"{{\"user\":\"{User.Identity?.Name}\"}}", outputData: null, status: "OK");
        
        await _whatsAppService.DisconnectAsync();
        
        await _logService.LogProcessStepAsync(traceId, 2, "Disconnect Tamamlandı", "WhatsAppController.Disconnect",
            inputData: null, outputData: null, status: "OK");
        
        return NoContent();
    }
}
