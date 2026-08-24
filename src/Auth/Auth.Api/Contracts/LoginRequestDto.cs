using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

/// <example>{"username":"example-maintainer","password":"example-password"}</example>
[Description("Credentials submitted to obtain an access token.")]
public sealed class LoginRequestDto
{
    public const int MaxUsernameLength = 100;
    public const int MaxPasswordLength = 200;

    [Description("Account username.")]
    [Required]
    [MaxLength(MaxUsernameLength)]
    public string Username { get; set; } = string.Empty;

    [Description("Account password.")]
    [Required]
    [MaxLength(MaxPasswordLength)]
    public string Password { get; set; } = string.Empty;
}
