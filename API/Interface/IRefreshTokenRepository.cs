using API.Entities;

namespace API.Interface;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task AddAsync(RefreshToken token);
    Task UpdateAsync(RefreshToken token);
    Task RevokeUserTokensAsync(string username);
}
