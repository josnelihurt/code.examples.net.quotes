using Auth.Application;
using Auth.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICredentialStore, HardcodedCredentialStore>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IAuthService, AuthService>();
        return services;
    }
}
