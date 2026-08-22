using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using ErrorOr;

namespace Auth.Application;

public sealed class AuthService(
    ICredentialStore credentials,
    ITokenService tokens) : IAuthService
{
    public async Task<ErrorOr<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!await credentials.ValidateAsync(request.Username, request.Password, cancellationToken))
        {
            return AuthErrors.InvalidCredentials;
        }

        var issued = await tokens.CreateTokenAsync(request.Username, cancellationToken);
        return new LoginResult(issued.AccessToken, request.Username, issued.ExpiresInSeconds);
    }

    public Task<ValidateResult> ValidateAsync(string accessToken, CancellationToken cancellationToken) =>
        tokens.ValidateTokenAsync(accessToken, cancellationToken);
}
