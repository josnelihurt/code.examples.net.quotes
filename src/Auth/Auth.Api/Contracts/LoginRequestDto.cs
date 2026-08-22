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
