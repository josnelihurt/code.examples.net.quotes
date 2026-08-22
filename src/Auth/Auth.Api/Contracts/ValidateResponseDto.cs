using System.ComponentModel;

namespace Auth.Api.Contracts;

/// <example>{"valid":true,"username":"jrb"}</example>
[Description("Result of validating an access token; returned for both success and failure.")]
public sealed class ValidateResponseDto
{
    [Description("True when the token is valid; false on unauthorized responses.")]
    public required bool Valid { get; init; }

    [Description("Username from a valid token; omitted or null when invalid.")]
    public string? Username { get; init; }
}
