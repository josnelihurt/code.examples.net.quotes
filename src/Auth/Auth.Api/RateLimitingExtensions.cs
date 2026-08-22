using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Auth.Api;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 30;
}

public static class RateLimitingExtensions
{
    public const string AuthPolicyName = "auth-endpoints";

    /// <summary>errorCode carried by the 429 problem when the auth rate limit trips.</summary>
    public const string RateLimitedErrorCode = "auth.rate_limited";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Fixed-window limiter over the public auth endpoints, partitioned per client IP.
    /// Login and token validation are unauthenticated oracles; throttling them is part
    /// of the seed's standing security posture, not an optional hardening step.
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "The auth endpoint rate limit was exceeded; retry after the window elapses.",
                    Extensions =
                    {
                        ["correlationId"] = context.HttpContext.GetCorrelationId(),
                        ["errorCode"] = RateLimitedErrorCode
                    }
                };

                await response.WriteAsync(JsonSerializer.Serialize(problem, _jsonOptions), cancellationToken);
            };

            options.AddPolicy(AuthPolicyName, context =>
            {
                var settings = context.RequestServices
                    .GetRequiredService<IOptions<AuthRateLimitOptions>>()
                    .Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}
