using API.DTOs;

namespace API.Interface;

public interface IAccountService
{
    Task<(UserDto User, string RefreshToken)> RegisterAsync(RegisterDto registerDto);
    Task<(UserDto User, string RefreshToken)> LoginAsync(LoginDto loginDto);
    Task<(UserDto User, string RefreshToken)> RefreshTokenAsync(string? refreshToken);
    Task RevokeRefreshTokenAsync(string? refreshToken);
}
