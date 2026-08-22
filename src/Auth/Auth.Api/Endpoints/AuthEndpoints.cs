using AspireQuotesPoc.ServiceDefaults.Http;
using AspireQuotesPoc.ServiceDefaults.Telemetry;
using AspireQuotesPoc.ServiceDefaults.Validation;
using Auth.Api.Contracts;
using Auth.Application.Abstractions;

namespace Auth.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .AddEndpointFilter<ValidationEndpointFilter<LoginRequestDto>>()
            .Produces<LoginResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/validate", Validate)
            .WithName("ValidateToken")
            .Produces<ValidateResponseDto>(StatusCodes.Status200OK)
            .Produces<ValidateResponseDto>(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    internal static async Task<IResult> LoginAsync(
        LoginRequestDto body,
        IAuthService authService,
        HttpContext http,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(AuthEndpoints));
        var correlationId = http.GetCorrelationId();
        logger.LogInformation("Login attempt for user {Username}", body.Username);

        var result = await authService.LoginAsync(
            new LoginRequest(body.Username, body.Password),
            cancellationToken);
        if (result.IsError)
        {
            AppMetrics.Record(AppMetrics.AuthLoginCount, "failure");
            logger.LogWarning("Login failed for user {Username}", body.Username);
            return result.Errors.ToProblem(http);
        }

        AppMetrics.Record(AppMetrics.AuthLoginCount, "success");
        logger.LogInformation("Login succeeded for user {Username}", result.Value.Username);
        return Results.Ok(new LoginResponseDto
        {
            AccessToken = result.Value.AccessToken,
            CorrelationId = correlationId,
            ExpiresIn = result.Value.ExpiresIn,
            Username = result.Value.Username
        });
    }

    internal static IResult Validate(
        ValidateRequestDto? body,
        IAuthService authService,
        HttpContext http,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(AuthEndpoints));
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
            return Unauthorized();
        }

        var result = authService.Validate(token);
        if (!result.Valid)
        {
            AppMetrics.Record(AppMetrics.AuthValidateCount, "failure");
            logger.LogWarning("Token validation failed");
            return Unauthorized();
        }

        AppMetrics.Record(AppMetrics.AuthValidateCount, "success");
        logger.LogInformation("Token validated for user {Username}", result.Username);
        return Results.Ok(new ValidateResponseDto { Valid = true, Username = result.Username });
    }

    private static IResult Unauthorized() => Results.Json(
        new ValidateResponseDto { Valid = false },
        statusCode: StatusCodes.Status401Unauthorized);
}
