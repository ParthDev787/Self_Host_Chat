using API.Entities;

namespace API.Interface;

public interface ITokenService
{
    string CreateAccessToken(AppUser user);
    RefreshToken CreateRefreshToken(string username);
}
