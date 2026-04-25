using CafeBot.Business.DTOs;
using CafeBot.Data.Context;
using CafeBot.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SuperAdminController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _dbContext;

    public SuperAdminController(UserManager<AppUser> userManager, AppDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var configs = await _dbContext.Configurations.IgnoreQueryFilters().ToListAsync();

        var dtoList = new List<UserAdminDto>();

        foreach (var user in users)
        {
            var config = configs.FirstOrDefault(c => c.UserId == user.Id);
            
            dtoList.Add(new UserAdminDto
            {
                Id = user.Id,
                Email = user.Email ?? "Bilinmiyor",
                CompanyName = user.CompanyName,
                CreatedAt = user.CreatedAt,
                ConnectionStatus = config?.ConnectionStatus.ToString() ?? "Bilinmiyor",
                SystemStatus = config?.SystemStatus.ToString() ?? "Bilinmiyor",
                IsBanned = user.IsBanned,
                SelectedGroupCount = config != null && !string.IsNullOrEmpty(config.TargetGroupIdsJson) 
                    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(config.TargetGroupIdsJson)?.Count ?? 0 
                    : 0
            });
        }

        return Ok(dtoList);
    }

    [HttpPost("users/{userId}/ban")]
    public async Task<IActionResult> BanUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

        user.IsBanned = true;
        await _userManager.UpdateAsync(user);

        // Sistemini durdur
        var config = await _dbContext.Configurations.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.UserId == userId);
        if (config != null)
        {
            config.SystemStatus = Data.Enums.SystemStatus.Stopped;
            await _dbContext.SaveChangesAsync();
        }

        return Ok(new { message = $"{user.Email} yasaklandı." });
    }

    [HttpPost("users/{userId}/unban")]
    public async Task<IActionResult> UnbanUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { message = "Kullanıcı bulunamadı." });

        user.IsBanned = false;
        await _userManager.UpdateAsync(user);
        return Ok(new { message = $"{user.Email} yasağı kaldırıldı." });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        var bannedUsers = await _userManager.Users.CountAsync(u => u.IsBanned);
        var configs = await _dbContext.Configurations.IgnoreQueryFilters().ToListAsync();
        var connectedUsers = configs.Count(c => c.ConnectionStatus == Data.Enums.ConnectionStatus.Connected);
        var runningUsers = configs.Count(c => c.SystemStatus == Data.Enums.SystemStatus.Running);

        return Ok(new
        {
            TotalUsers = totalUsers,
            BannedUsers = bannedUsers,
            ConnectedUsers = connectedUsers,
            RunningUsers = runningUsers
        });
    }
}
