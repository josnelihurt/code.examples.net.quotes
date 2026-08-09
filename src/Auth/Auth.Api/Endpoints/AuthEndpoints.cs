using AspireQuotesPoc.Http;
using AspireQuotesPoc.Telemetry;
using Auth.Api.Contracts;
using Auth.Application;

namespace Auth.Api.Endpoints;

/// <summary>
/// Route registration and handlers for <c>/api/auth</c>. Not static so the handlers can take an
/// <see cref="ILogger{TCategoryName}"/> under this category.
/// </summary>
public sealed class AuthEndpoints
{
    private AuthEndpoints()
    {
    }

    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResponseDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponseDto>(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

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
        ILogger<AuthEndpoints> logger)
    {
        var validation = await ValidationFilter.ValidateAsync(body, http);
        if (validation is not null)
        {
            return validation;
        }

        var correlationId = http.GetCorrelationId();
        logger.LogInformation("Login attempt for user {Username}", body.Username);

        var result = authService.Login(new LoginRequest(body.Username, body.Password));
        if (result is null)
        {
            AppMetrics.Record(AppMetrics.AuthLoginCount, "failure");
            logger.LogWarning("Login failed for user {Username}", body.Username);
            return Results.Json(
                new ErrorResponseDto { Error = "Invalid credentials" },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        AppMetrics.Record(AppMetrics.AuthLoginCount, "success");
        logger.LogInformation("Login succeeded for user {Username}", result.Username);
        return Results.Ok(new LoginResponseDto
        {
            AccessToken = result.AccessToken,
            CorrelationId = correlationId,
            ExpiresIn = result.ExpiresIn,
            Username = result.Username
        });
    }

    internal static IResult Validate(
        ValidateRequestDto? body,
        IAuthService authService,
        HttpContext http,
        ILogger<AuthEndpoints> logger)
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
