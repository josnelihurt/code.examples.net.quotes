using Auth.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer's own services. Each layer owns its registrations;
    /// the API host composes them and is the single composition root.
    /// </summary>
    public static IServiceCollection AddAuthApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAuthService, AuthService>();
        return services;
    }
}
