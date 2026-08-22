using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Auth.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the infrastructure adapters only. The application services are
    /// registered by <c>AddAuthApplication</c>; the API host composes both.
    /// </summary>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "The local scaffolding credential store must not run in Production; register a real ICredentialStore adapter.");
        }

        services.AddSingleton<ICredentialStore, HardcodedCredentialStore>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }
}
