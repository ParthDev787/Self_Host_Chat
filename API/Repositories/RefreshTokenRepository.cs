using API.Data;
using API.Entities;
using API.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Repositories;

public class RefreshTokenRepository(DataContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
    }

    public async Task AddAsync(RefreshToken token)
    {
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(RefreshToken token)
    {
        context.RefreshTokens.Update(token);
        await context.SaveChangesAsync();
    }

    public async Task RevokeUserTokensAsync(string username)
    {
        var tokens = await context.RefreshTokens
            .Where(r => r.Username == username && r.RevokedAt == null)
            .ToListAsync();

        if (tokens.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var t in tokens)
            {
                t.RevokedAt = now;
            }
            await context.SaveChangesAsync();
        }
    }
}
