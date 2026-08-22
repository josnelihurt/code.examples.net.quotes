using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

[Description("Optional body for token validation; Authorization Bearer may be used instead.")]
public sealed class ValidateRequestDto
{
    [Description("Access token to validate when not supplied via Authorization header.")]
    [MaxLength(4096)]
    public string? AccessToken { get; set; }
}
