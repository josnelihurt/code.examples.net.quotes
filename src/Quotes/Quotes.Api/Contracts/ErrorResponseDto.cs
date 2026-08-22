using System.ComponentModel;

namespace Quotes.Api.Contracts;

[Description("Error payload returned when the request is not authorized.")]
public sealed class ErrorResponseDto
{
    [Description("Human-readable reason the request was rejected.")]
    public required string Error { get; init; }
}
