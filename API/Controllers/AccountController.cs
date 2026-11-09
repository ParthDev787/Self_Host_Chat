using System.Security.Claims;
using API.DTOs;
using API.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

[EnableRateLimiting("AuthLimiter")]
public class AccountController(IAccountService accountService) : BaseApiController
{
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
    {
        var (user, refreshToken) = await accountService.RegisterAsync(registerDto);
        SetRefreshTokenCookie(refreshToken);
        return Ok(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
    {
        var (user, refreshToken) = await accountService.LoginAsync(loginDto);
        SetRefreshTokenCookie(refreshToken);
        return Ok(user);
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<UserDto>> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        var (user, newRefreshToken) = await accountService.RefreshTokenAsync(refreshToken);
        SetRefreshTokenCookie(newRefreshToken);
        return Ok(user);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        await accountService.RevokeRefreshTokenAsync(refreshToken);
        DeleteRefreshTokenCookie();
        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username))
            return BadRequest(new { message = "Username is required" });

        var currentUsername = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (currentUsername != null && !string.Equals(currentUsername, dto.Username.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await accountService.UpdateThemeAsync(dto.Username.Trim(), dto.Theme);
        return Ok(new { message = "Theme updated successfully", theme = dto.Theme });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(30),
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            IsEssential = true
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            IsEssential = true
        });
    }
}
