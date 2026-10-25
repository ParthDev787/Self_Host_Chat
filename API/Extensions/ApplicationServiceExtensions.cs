using API.Interface;
using API.Repositories;
using API.Services;

namespace API.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // ── Repositories ──
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IKeyRepository, KeyRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // ── Services ──
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IConnectionService, ConnectionService>();

        return services;
    }
}
