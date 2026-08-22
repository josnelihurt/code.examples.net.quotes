using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using ErrorOr;

namespace Auth.Application;

public sealed class AuthService(
    ICredentialStore credentials,
    ITokenService tokens) : IAuthService
{
    public Task<ErrorOr<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Task.FromResult<ErrorOr<LoginResult>>(AuthErrors.InvalidCredentials);
        }

        if (!credentials.Validate(request.Username, request.Password))
        {
            return Task.FromResult<ErrorOr<LoginResult>>(AuthErrors.InvalidCredentials);
        }

        var token = tokens.CreateToken(request.Username, out var expiresInSeconds);
        return Task.FromResult<ErrorOr<LoginResult>>(
            new LoginResult(token, request.Username, expiresInSeconds));
    }

    public ValidateResult Validate(string accessToken) => tokens.ValidateToken(accessToken);
}
