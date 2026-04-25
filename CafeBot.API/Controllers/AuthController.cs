using CafeBot.Data.Entities;
using CafeBot.Business.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CafeBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _configuration;

    public AuthController(UserManager<AppUser> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Email veya şifre hatalı." });
        }

        var token = await GenerateJwtTokenAsync(user);
        return Ok(new AuthResponseDto { Token = token, Email = user.Email!, CompanyName = user.CompanyName });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (await _userManager.FindByEmailAsync(request.Email) != null)
        {
            return BadRequest(new { message = "Bu email adresi zaten kullanılıyor." });
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            CompanyName = request.CompanyName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (result.Succeeded)
        {
            // Eğer sistemdeki ilk kullanıcı ise Admin rolü ver
            if (_userManager.Users.Count() == 1)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
            }

            var token = await GenerateJwtTokenAsync(user);
            return Ok(new AuthResponseDto { Token = token, Email = user.Email!, CompanyName = user.CompanyName });
        }

        return BadRequest(new { message = "Kullanıcı oluşturulamadı.", errors = result.Errors.Select(e => e.Description) });
    }

    private async Task<string> GenerateJwtTokenAsync(AppUser user)
    {
        var jwtSecret = _configuration["JwtSettings:Secret"] ?? "CafeBotSuperSecretKey_NeedsToBeLongEnoughForHS256!";
        var jwtIssuer = _configuration["JwtSettings:Issuer"] ?? "CafeBotApi";
        var jwtAudience = _configuration["JwtSettings:Audience"] ?? "CafeBotUsers";

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id)
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (!string.IsNullOrEmpty(user.CompanyName))
        {
            claims.Add(new Claim("company", user.CompanyName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["JwtSettings:ExpirationDays"] ?? "7"));

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
