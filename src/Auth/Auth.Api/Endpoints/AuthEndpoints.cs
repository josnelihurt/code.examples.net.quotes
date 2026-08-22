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
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // RFC 7662-style introspection: both "valid" and "invalid" are successful answers
        // (200 with the flag); only a missing token is a request error (400).
        auth.MapPost("/validate", ValidateAsync)
            .WithName("ValidateToken")
            .Produces<ValidateResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    internal static async Task<IResult> LoginAsync(
        LoginRequestDto body,
        IAuthService authService,
        HttpContext http,
        ILogger<AuthEndpointsLog> logger,
        CancellationToken cancellationToken)
    {
        var correlationId = http.GetCorrelationId();
        logger.LogInformation("Login attempt");

        var result = await authService.LoginAsync(
            new LoginRequest(body.Username, body.Password),
            cancellationToken);
        if (result.IsError)
        {
            // Credentials are user input: never log the values, only the outcome.
            AppMetrics.Record(AppMetrics.AuthLoginCount, "failure");
            logger.LogWarning("Login failed");
            return result.Errors.ToProblem(http);
        }

        AppMetrics.Record(AppMetrics.AuthLoginCount, "success");
        logger.LogInformation("Login succeeded");
        return Results.Ok(new LoginResponseDto
        {
            AccessToken = result.Value.AccessToken,
            CorrelationId = correlationId,
            ExpiresIn = result.Value.ExpiresIn,
            Username = result.Value.Username
        });
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
            AppMetrics.Record(AppMetrics.AuthValidateCount, "failure");
            logger.LogWarning("Token validation request carried no token");
            return AuthErrors.MissingToken.ToProblem(http);
        }

        var result = await authService.ValidateAsync(token, cancellationToken);
        if (!result.Valid)
        {
            AppMetrics.Record(AppMetrics.AuthValidateCount, "failure");
            logger.LogWarning("Token validation failed");
            return Results.Ok(new ValidateResponseDto { Valid = false });
        }

        AppMetrics.Record(AppMetrics.AuthValidateCount, "success");
        logger.LogInformation("Token validated for user {Username}", result.Username);
        return Results.Ok(new ValidateResponseDto { Valid = true, Username = result.Username });
    }
}
