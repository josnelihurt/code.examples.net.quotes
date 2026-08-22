using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure adapters only. The application services are
    /// registered by <c>AddAuthApplication</c>; the API host composes both.
    /// </summary>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICredentialStore, HardcodedCredentialStore>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }
}
