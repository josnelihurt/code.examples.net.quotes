using AspireQuotesPoc.ServiceDefaults.Telemetry;
using Auth.Application.Abstractions;
using ErrorOr;

namespace Auth.Api.Telemetry;

/// <summary>
/// Metrics leg of the auth decorator chain: one increment per login attempt and per
/// token validation, tagged with the plain success/failure outcome (auth counters do
/// not use the quotes outcome vocabulary).
/// </summary>
internal sealed class AuthServiceTelemetry(IAuthService inner) : IAuthService
{
    public async Task<ErrorOr<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await inner.LoginAsync(request, cancellationToken);
        AppMetrics.Record(
            AppMetrics.AuthLoginCount,
            result.MatchFirst(onValue: _ => "success", onFirstError: _ => "failure"));
        return result;
    }

    public async Task<ValidateResult> ValidateAsync(string accessToken, CancellationToken cancellationToken)
    {
        var result = await inner.ValidateAsync(accessToken, cancellationToken);
        AppMetrics.Record(AppMetrics.AuthValidateCount, result.Valid ? "success" : "failure");
        return result;
    }
}
