using CafeBot.Business.DTOs;
using CafeBot.Data.Entities;
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

    public UserController(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound("Kullanıcı bulunamadı.");

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            CompanyName = user.CompanyName,
            PersonalGeminiApiKey = user.PersonalGeminiApiKey
        });
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
            return Ok();

        return BadRequest(result.Errors);
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

