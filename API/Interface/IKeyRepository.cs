using API.Entities;

namespace API.Interface;

public interface IKeyRepository
{
    Task<UserPublicKey?> GetByUsernameAsync(string username);
    Task SaveKeyAsync(UserPublicKey key);
}
