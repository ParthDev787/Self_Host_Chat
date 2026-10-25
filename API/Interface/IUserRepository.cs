using API.Entities;

namespace API.Interface;

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username);
    Task<bool> ExistsAsync(string username);
    Task AddAsync(AppUser user);
}
