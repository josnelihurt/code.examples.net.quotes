using Auth.Application;
using Auth.Application.Abstractions;

namespace Auth.Api.Telemetry;

public static class AuthServiceTelemetryExtensions
{
    /// <summary>
    /// Wraps the auth service in the telemetry/logging decorator chain (telemetry
    /// outermost, then logging, then the service), preserving its singleton lifetime.
    /// These registrations resolve ahead of <c>AddAuthApplication</c>'s because the last
    /// registration of a service type wins.
    /// </summary>
    public static IServiceCollection AddAuthServiceTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<AuthService>();
        services.AddSingleton<IAuthService>(sp => new AuthServiceTelemetry(
            new AuthServiceLogging(
                sp.GetRequiredService<AuthService>(),
                sp.GetRequiredService<ILogger<AuthServiceLogging>>())));
        return services;
    }
}
