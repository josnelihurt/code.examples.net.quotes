using AspireQuotesPoc.ServiceDefaults.Http;
using AspireQuotesPoc.ServiceDefaults.Telemetry;
using Auth.Api.Contracts;
using Auth.Application.Abstractions;

namespace Auth.Api.Endpoints;

/// <summary>Logger category for auth endpoint handlers (static types cannot be used as ILogger&lt;T&gt; arguments).</summary>
internal sealed class AuthEndpointsLog;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth").RequireRateLimiting(RateLimitingExtensions.AuthPolicyName);

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // RFC 7662-style introspection: both "valid" and "invalid" are successful answers
        // (200 with the flag); only a missing token is a request error (400).
        auth.MapPost("/validate", ValidateAsync)
            .WithName("ValidateToken")
            .Produces<ValidateResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    internal static async Task<IResult> LoginAsync(
        LoginRequestDto body,
        IAuthService authService,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var correlationId = http.GetCorrelationId();

        var result = await authService.LoginAsync(
            new LoginRequest(body.Username, body.Password),
            cancellationToken);
        return result.Match(
            onValue: value => Results.Ok(new LoginResponseDto
            {
                AccessToken = value.AccessToken,
                CorrelationId = correlationId,
                ExpiresIn = value.ExpiresIn,
                Username = value.Username
            }),
            onError: errors => errors.ToProblem(http));
    }

    internal static async Task<IResult> ValidateAsync(
        ValidateRequestDto? body,
        IAuthService authService,
        HttpContext http,
        ILogger<AuthEndpointsLog> logger,
        CancellationToken cancellationToken)
    {
        var token = body?.AccessToken;
        if (string.IsNullOrWhiteSpace(token)
            && BearerToken.TryParse(http.Request.Headers.Authorization.FirstOrDefault(), out var headerToken))
        {
            token = headerToken;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            // Bearer parsing is an API concern, so this pre-service failure cannot move
            // into the decorators: record it here, before the auth service is involved.
            AppMetrics.Record(AppMetrics.AuthValidateCount, "failure");
            logger.LogWarning("Token validation request carried no token");
            return AuthErrors.MissingToken.ToProblem(http);
        }

        var result = await authService.ValidateAsync(token, cancellationToken);
        return result.Valid
            ? Results.Ok(new ValidateResponseDto { Valid = true, Username = result.Username })
            : Results.Ok(new ValidateResponseDto { Valid = false });
    }
}
