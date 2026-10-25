using API.Data;
using API.Entities;
using API.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Repositories;

public class KeyRepository(DataContext context) : IKeyRepository
{
    public async Task<UserPublicKey?> GetByUsernameAsync(string username)
    {
        return await context.UserPublicKeys.FirstOrDefaultAsync(k => k.Username == username);
    }

    public async Task SaveKeyAsync(UserPublicKey key)
    {
        var existing = await context.UserPublicKeys.FirstOrDefaultAsync(k => k.Username == key.Username);
        if (existing == null)
        {
            context.UserPublicKeys.Add(key);
        }
        else
        {
            existing.PublicKeyJwk = key.PublicKeyJwk;
            existing.PreKeyJwk = key.PreKeyJwk;
            existing.UpdatedAt = key.UpdatedAt;
        }

        await context.SaveChangesAsync();
    }
}
