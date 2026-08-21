using System.ComponentModel;

namespace Auth.Api.Contracts;

[Description("Credentials submitted to obtain an access token.")]
public sealed class LoginRequestDto
{
    [Description("Account username.")]
    public string Username { get; set; } = string.Empty;

    [Description("Account password.")]
    public string Password { get; set; } = string.Empty;
}

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

[Description("Error payload returned when authentication fails.")]
public sealed class ErrorResponseDto
{
    [Description("Human-readable reason authentication failed.")]
    public required string Error { get; init; }
}

[Description("Optional body for token validation; Authorization Bearer may be used instead.")]
public sealed class ValidateRequestDto
{
    [Description("Access token to validate when not supplied via Authorization header.")]
    public string? AccessToken { get; set; }
}

[Description("Result of validating an access token; returned for both success and failure.")]
public sealed class ValidateResponseDto
{
    [Description("True when the token is valid; false on unauthorized responses.")]
    public required bool Valid { get; init; }

    [Description("Username from a valid token; omitted or null when invalid.")]
    public string? Username { get; init; }
}
