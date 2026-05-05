using CafeBot.Business.DTOs;
using CafeBot.Data.Entities;
using CafeBot.Business.Infrastructure.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly GeminiClient _geminiClient;

    public UserController(UserManager<AppUser> userManager, GeminiClient geminiClient)
    {
        _userManager = userManager;
        _geminiClient = geminiClient;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile([FromServices] CafeBot.Business.Interfaces.ILogService logService)
    {
        var traceId = Guid.NewGuid().ToString("N")[..8];
        var stepOrder = 0;
        
        Console.WriteLine($"🔍 [TRACE:{traceId}] [UserController] GetProfile endpoint çağrıldı");
        
        try
        {
            // Step 1: Kullanıcı doğrulama
            stepOrder++;
            await logService.LogProcessStepAsync(traceId, stepOrder, "Kullanıcı Doğrulama", "GetUserAsync", "User.Identity", null, "BAŞLADI");
            
            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
            {
                Console.WriteLine($"❌ [TRACE:{traceId}] [UserController] Kullanıcı bulunamadı");
                await logService.LogProcessStepAsync(traceId, stepOrder, "Kullanıcı Doğrulama", "GetUserAsync", "User.Identity", "Kullanıcı bulunamadı", "HATA");
                return NotFound("Kullanıcı bulunamadı.");
            }

            Console.WriteLine($"✅ [TRACE:{traceId}] [UserController] Kullanıcı bulundu - Email: {user.Email}");
            await logService.LogProcessStepAsync(traceId, stepOrder, "Kullanıcı Doğrulama", "GetUserAsync", "User.Identity", $"Email: {user.Email}", "BAŞARILI");

            // Step 2: API Key durumu kontrolü
            stepOrder++;
            var hasApiKey = !string.IsNullOrWhiteSpace(user.PersonalGeminiApiKey);
            var apiKeyStatus = hasApiKey ? $"MEVCUT ({user.PersonalGeminiApiKey.Length} karakter)" : "BOŞ/NULL";
            
            Console.WriteLine($"🔑 [TRACE:{traceId}] [UserController] PersonalGeminiApiKey durumu: {apiKeyStatus}");
            await logService.LogProcessStepAsync(traceId, stepOrder, "API Key Kontrolü", "PersonalGeminiApiKey Check", user.Email, apiKeyStatus, "TAMAMLANDI");
            
            if (hasApiKey)
            {
                Console.WriteLine($"🔍 [TRACE:{traceId}] [UserController] API Key değeri: {user.PersonalGeminiApiKey.Substring(0, Math.Min(10, user.PersonalGeminiApiKey.Length))}...");
            }

            // Step 3: DTO oluşturma
            stepOrder++;
            await logService.LogProcessStepAsync(traceId, stepOrder, "DTO Oluşturma", "UserProfileDto Creation", apiKeyStatus, null, "BAŞLADI");
            
            var profileDto = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email ?? "",
                CompanyName = user.CompanyName,
                PersonalGeminiApiKey = user.PersonalGeminiApiKey
            };

            var dtoSummary = $"Email: {profileDto.Email}, HasApiKey: {!string.IsNullOrWhiteSpace(profileDto.PersonalGeminiApiKey)}";
            Console.WriteLine($"📤 [TRACE:{traceId}] [UserController] Profile DTO oluşturuldu - {dtoSummary}");
            await logService.LogProcessStepAsync(traceId, stepOrder, "DTO Oluşturma", "UserProfileDto Creation", apiKeyStatus, dtoSummary, "BAŞARILI");
            
            // Step 4: Response döndürme
            stepOrder++;
            await logService.LogProcessStepAsync(traceId, stepOrder, "Response Döndürme", "Ok(profileDto)", dtoSummary, "HTTP 200 OK", "TAMAMLANDI");
            
            Console.WriteLine($"🎯 [TRACE:{traceId}] [UserController] Profile endpoint başarıyla tamamlandı");
            
            return Ok(profileDto);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 [TRACE:{traceId}] [UserController] GetProfile exception: {ex.Message}");
            
            stepOrder++;
            await logService.LogProcessStepAsync(traceId, stepOrder, "Exception Handling", "Exception Catch", null, ex.Message, "EXCEPTION");
            
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto dto)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound("Kullanıcı bulunamadı.");

        user.CompanyName = dto.CompanyName;
        user.PersonalGeminiApiKey = dto.PersonalGeminiApiKey;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Başarı durumunda API key durumunu da döndür
            return Ok(new { 
                Message = "Profil başarıyla güncellendi.", 
                HasApiKey = !string.IsNullOrWhiteSpace(user.PersonalGeminiApiKey),
                ApiKeyLength = user.PersonalGeminiApiKey?.Length ?? 0
            });
        }

        return BadRequest(result.Errors);
    }

    [HttpPost("validate-api-key")]
    public async Task<IActionResult> ValidateApiKey([FromBody] ValidateApiKeyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ApiKey))
        {
            return BadRequest(new { IsValid = false, Error = "API anahtarı boş olamaz." });
        }

        try
        {
            // Gemini API anahtarını test et
            var isValid = await _geminiClient.TestApiKeyAsync(dto.ApiKey);
            
            if (isValid)
            {
                return Ok(new { IsValid = true, Message = "API anahtarı geçerli ve çalışıyor." });
            }
            else
            {
                return Ok(new { IsValid = false, Error = "API anahtarı geçersiz veya çalışmıyor." });
            }
        }
        catch (Exception ex)
        {
            return Ok(new { IsValid = false, Error = $"API anahtarı test edilirken hata oluştu: {ex.Message}" });
        }
    }

    [HttpGet("diagnose-gemini")]
    [AllowAnonymous] // Allow anonymous access for diagnostic purposes
    public async Task<IActionResult> DiagnoseGemini()
    {
        var diagnostics = new
        {
            Timestamp = DateTime.UtcNow,
            Environment = new
            {
                IsDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true",
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount
            },
            Network = new
            {
                // Try to resolve Google's DNS
                CanResolveGoogle = await TryResolveHostAsync("google.com"),
                CanResolveGeminiApi = await TryResolveHostAsync("generativelanguage.googleapis.com"),
                // Test basic HTTP connectivity
                CanConnectToGoogle = await TryHttpConnectAsync("https://google.com"),
                CanConnectToGeminiApi = await TryHttpConnectAsync("https://generativelanguage.googleapis.com")
            },
            GeminiConfig = new
            {
                BaseUrl = "https://generativelanguage.googleapis.com",
                HasApiKey = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Gemini__ApiKey")),
                Model = "gemini-2.5-flash"
            }
        };

        return Ok(diagnostics);
    }

    private async Task<bool> TryResolveHostAsync(string hostname)
    {
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(hostname);
            return addresses.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryHttpConnectAsync(string url)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync(url);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Forbidden; // 403 is still a connection
        }
        catch
        {
            return false;
        }
    }

    [HttpGet("debug-user-info")]
    [AllowAnonymous] // Geçici debug için
    public async Task<IActionResult> DebugUserInfo()
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
            {
                return Ok(new { 
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    UserName = User.Identity?.Name,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList(),
                    Message = "User not found in database"
                });
            }

            return Ok(new { 
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                HasApiKey = !string.IsNullOrWhiteSpace(user.PersonalGeminiApiKey),
                ApiKeyLength = user.PersonalGeminiApiKey?.Length ?? 0,
                IsAdmin = User.IsInRole("Admin"),
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
        catch (Exception ex)
        {
            return Ok(new { Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    [HttpGet("has-valid-api-key")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> HasValidApiKey()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound("Kullanıcı bulunamadı.");

        var hasApiKey = !string.IsNullOrWhiteSpace(user.PersonalGeminiApiKey);
        
        return Ok(new { 
            HasApiKey = hasApiKey,
            IsAdmin = User.IsInRole("Admin"),
            CanUseSystemKey = User.IsInRole("Admin") // Admin'ler sistem anahtarını kullanabilir
        });
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount([FromServices] CafeBot.Business.Interfaces.IWhatsAppService whatsAppService, [FromServices] CafeBot.Data.Context.AppDbContext dbContext)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound("Kullanıcı bulunamadı.");

        try
        {
            await whatsAppService.DisconnectAsync();
        }
        catch (System.Exception) { }

        var configs = dbContext.Configurations.Where(c => c.UserId == user.Id);
        dbContext.Configurations.RemoveRange(configs);
        await dbContext.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
            return Ok(new { message = "Hesap başarıyla silindi." });

        return BadRequest(result.Errors);
    }
}

