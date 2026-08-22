using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;

namespace Auth.Application;

public sealed class AuthService(ICredentialStore credentials, ITokenService tokens) : IAuthService
{
    private readonly ICredentialStore _credentials = credentials;
    private readonly ITokenService _tokens = tokens;

    public LoginResult? Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        if (!_credentials.Validate(request.Username, request.Password))
        {
            return null;
        }

        var token = _tokens.CreateToken(request.Username, out var expiresIn);
        return new LoginResult(token, request.Username, expiresIn);
    }

    public ValidateResult Validate(string accessToken) => _tokens.ValidateToken(accessToken);
}
