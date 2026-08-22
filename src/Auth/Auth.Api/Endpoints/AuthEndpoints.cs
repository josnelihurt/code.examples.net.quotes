using AspireQuotesPoc.ServiceDefaults.Http;
using AspireQuotesPoc.ServiceDefaults.Telemetry;
using Auth.Api.Contracts;
using Auth.Application.Abstractions;

namespace Auth.Api.Endpoints;

/// <summary>Logger category for auth endpoint handlers (static types cannot be used as ILogger&lt;T&gt; arguments).</summary>
internal sealed class AuthEndpointsLog;

public static class AuthEndpoints
{
    /// <summary>OpenAPI document this version publishes into. See <c>AddStandardApiServices</c>.</summary>
    internal const string DocumentName = "v1";

    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup($"/api/{DocumentName}/auth")
            .WithGroupName(DocumentName)
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicyName);

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

    /// <summary>Exchanges credentials for a JWT access token.</summary>
    /// <remarks>
    /// Typical use: call this first, then send the returned <c>accessToken</c> as
    /// <c>Authorization: Bearer</c> to the Quotes API, which authorizes against the
    /// <c>quotes:read</c> and <c>quotes:write</c> scope claims carried by the token.
    /// Login is rate limited per client IP (fixed window, 10 requests per 30 seconds by
    /// default); send <c>X-Correlation-Id</c> to reuse one correlation id across calls — it
    /// is echoed on every response and embedded in problem details.
    /// </remarks>
    /// <param name="authService">Application dependency, not part of the HTTP contract.</param>
    /// <param name="http">Request context, not part of the HTTP contract.</param>
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <param name="body">Credentials. Development users: <c>jrb/supersecret</c> (read + write scopes), <c>reader/readsecret</c> (read only).</param>
    /// <response code="200">Credentials accepted; the body carries the access token, its lifetime in seconds and the correlation id.</response>
    /// <response code="400">Malformed payload; transport validation errors are keyed by property name (Username, Password).</response>
    /// <response code="401">Unknown credentials (errorCode <c>auth.invalid_credentials</c>).</response>
    /// <response code="429">Rate limit exceeded (errorCode <c>auth.rate_limited</c>); retry after the window elapses.</response>
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

    /// <summary>Introspects an access token (RFC 7662 style).</summary>
    /// <remarks>
    /// Both a valid and an invalid token are successful answers: the response is always
    /// <c>200 { valid, username }</c> and only a missing token is a request error. The token
    /// is read from the JSON body when present, otherwise from the
    /// <c>Authorization: Bearer</c> header. Rate limited per client IP exactly like login
    /// (fixed window, 10 requests per 30 seconds by default).
    /// </remarks>
    /// <param name="body">Optional payload carrying the access token; omit it to introspect the bearer header instead.</param>
    /// <param name="authService">Application dependency, not part of the HTTP contract.</param>
    /// <param name="http">Request context, not part of the HTTP contract.</param>
    /// <param name="logger">Telemetry dependency, not part of the HTTP contract.</param>
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <response code="200">Introspection result. <c>valid</c> is false for unknown or expired tokens; <c>username</c> is only set for valid ones.</response>
    /// <response code="400">No token in the body or the Authorization header (errorCode <c>auth.token_missing</c>).</response>
    /// <response code="429">Rate limit exceeded (errorCode <c>auth.rate_limited</c>); retry after the window elapses.</response>
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
