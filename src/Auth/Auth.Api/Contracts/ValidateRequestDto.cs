using System.ComponentModel;

namespace Auth.Api.Contracts;

[Description("Optional body for token validation; Authorization Bearer may be used instead.")]
public sealed class ValidateRequestDto
{
    [Description("Access token to validate when not supplied via Authorization header.")]
    public string? AccessToken { get; set; }
}
