using API.Data;
using API.Entities;
using API.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Repositories;

public class UserRepository(DataContext context) : IUserRepository
{
    public async Task<AppUser?> GetByUsernameAsync(string username)
    {
        return await context.Users.FirstOrDefaultAsync(x => x.UserName == username);
    }

    public async Task<bool> ExistsAsync(string username)
    {
        return await context.Users.AnyAsync(x => x.UserName == username);
    }

    public async Task AddAsync(AppUser user)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }
}
