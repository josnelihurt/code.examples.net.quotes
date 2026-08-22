using System.ComponentModel;

namespace Auth.Api.Contracts;

[Description("Error payload returned when authentication fails.")]
public sealed class ErrorResponseDto
{
    [Description("Human-readable reason authentication failed.")]
    public required string Error { get; init; }
}
