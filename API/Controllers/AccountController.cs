using API.DTOs;
using API.Interface;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

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

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(1),
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
