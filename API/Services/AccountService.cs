using API.DTOs;
using API.Entities;
using API.Errors;
using API.Interface;
using Microsoft.AspNetCore.Identity;

namespace API.Services;

public class AccountService(
    IUserRepository userRepo,
    IRefreshTokenRepository refreshTokenRepo,
    ITokenService tokenService) : IAccountService
{
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public async Task<(UserDto User, string RefreshToken)> RegisterAsync(RegisterDto dto)
    {
        if (await userRepo.ExistsAsync(dto.Username.ToLower()))
            throw new BadRequestException("Username is taken");

        var user = new AppUser
        {
            UserName = dto.Username.ToLower(),
            PasswordHash = string.Empty
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        await userRepo.AddAsync(user);

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user.UserName);

        await refreshTokenRepo.AddAsync(refreshToken);

        var userDto = new UserDto
        {
            Username = user.UserName,
            Token = accessToken,
            Theme = user.Theme ?? "dark"
        };

        return (userDto, refreshToken.Token);
    }

    public async Task<(UserDto User, string RefreshToken)> LoginAsync(LoginDto dto)
    {
        var user = await userRepo.GetByUsernameAsync(dto.Username.ToLower())
            ?? throw new UnauthorizedException("Invalid username");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid password");

        var accessToken = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken(user.UserName);

        await refreshTokenRepo.AddAsync(refreshToken);

        var userDto = new UserDto
        {
            Username = user.UserName,
            Token = accessToken,
            Theme = user.Theme ?? "dark"
        };

        return (userDto, refreshToken.Token);
    }

    public async Task<(UserDto User, string RefreshToken)> RefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Refresh token is required");

        var existingToken = await refreshTokenRepo.GetByTokenAsync(refreshToken);

        if (existingToken == null)
            throw new UnauthorizedException("Invalid refresh token");

        // Concurrency grace period: if token was rotated in the last 60 seconds by a parallel request, return the replacement token
        if (!existingToken.IsActive)
        {
            if (existingToken.RevokedAt != null && 
                existingToken.RevokedAt > DateTime.UtcNow.AddSeconds(-60) && 
                !string.IsNullOrEmpty(existingToken.ReplacedByToken))
            {
                var replacement = await refreshTokenRepo.GetByTokenAsync(existingToken.ReplacedByToken);
                if (replacement != null && replacement.IsActive)
                {
                    var existingUser = await userRepo.GetByUsernameAsync(replacement.Username);
                    if (existingUser != null)
                    {
                        var reissuedAccess = tokenService.CreateAccessToken(existingUser);
                        return (new UserDto
                        {
                            Username = existingUser.UserName,
                            Token = reissuedAccess,
                            Theme = existingUser.Theme ?? "dark"
                        }, replacement.Token);
                    }
                }
            }

            throw new UnauthorizedException("Invalid or expired refresh token");
        }

        var user = await userRepo.GetByUsernameAsync(existingToken.Username)
            ?? throw new UnauthorizedException("User no longer exists");

        // Generate new 10-minute access token and new 1-day refresh token (Rotation)
        var newAccessToken = tokenService.CreateAccessToken(user);
        var newRefreshToken = tokenService.CreateRefreshToken(user.UserName);

        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.ReplacedByToken = newRefreshToken.Token;

        await refreshTokenRepo.UpdateAsync(existingToken);
        await refreshTokenRepo.AddAsync(newRefreshToken);

        var userDto = new UserDto
        {
            Username = user.UserName,
            Token = newAccessToken,
            Theme = user.Theme ?? "dark"
        };

        return (userDto, newRefreshToken.Token);
    }

    public async Task RevokeRefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var token = await refreshTokenRepo.GetByTokenAsync(refreshToken);
        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTime.UtcNow;
            await refreshTokenRepo.UpdateAsync(token);
        }
    }

    public async Task UpdateThemeAsync(string username, string theme)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BadRequestException("Username is required");

        var user = await userRepo.GetByUsernameAsync(username.ToLower())
            ?? throw new NotFoundException("User not found");

        user.Theme = string.IsNullOrWhiteSpace(theme) ? "dark" : theme.Trim().ToLower();
        await userRepo.UpdateAsync(user);
    }
}
