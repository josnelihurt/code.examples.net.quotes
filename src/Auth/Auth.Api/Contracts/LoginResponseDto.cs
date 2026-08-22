using System.ComponentModel;

namespace Auth.Api.Contracts;

/// <example>{"accessToken":"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJyZWFkZXIifQ.x","correlationId":"5c1f4a0e9d2b7386a4c0b1e8d3f69a27","expiresIn":3600,"username":"jrb"}</example>
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
