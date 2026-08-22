using System.ComponentModel;

namespace Auth.Api.Contracts;

[Description("Successful login result including the issued access token.")]
public sealed class LoginResponseDto
{
    [Description("JWT access token for authenticated API calls.")]
    public required string AccessToken { get; init; }

    [Description("Correlation id for this login; clients should send it as X-Correlation-Id on later calls.")]
    public required string CorrelationId { get; init; }

    [Description("Access token lifetime in seconds.")]
    public required int ExpiresIn { get; init; }

    [Description("Authenticated username.")]
    public required string Username { get; init; }
}
