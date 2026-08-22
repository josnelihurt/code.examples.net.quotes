using Auth.Application.Abstractions;
using ErrorOr;

namespace Auth.Api.Telemetry;

/// <summary>Logging leg of the auth decorator chain: login attempt/outcome and token validation outcome.</summary>
internal sealed class AuthServiceLogging(
    IAuthService inner,
    ILogger<AuthServiceLogging> logger) : IAuthService
{
    public async Task<ErrorOr<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // Credentials are user input: never log the values, only the outcome.
        logger.LogInformation("Login attempt");

        var result = await inner.LoginAsync(request, cancellationToken);
        result.SwitchFirst(
            onValue: _ => logger.LogInformation("Login succeeded"),
            onFirstError: _ => logger.LogWarning("Login failed"));
        return result;
    }

    public async Task<ValidateResult> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        // ValidateResult is not an ErrorOr, so Switch/Match do not apply; the guard
        // clause keeps the branch else-free.
        var result = await inner.ValidateAsync(accessToken, cancellationToken);

        if (!result.Valid)
        {
            logger.LogWarning("Token validation failed");
            return result;
        }

        logger.LogInformation("Token validated for user {Username}", result.Username);
        return result;
    }
}
