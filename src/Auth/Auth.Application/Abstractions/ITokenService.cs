namespace Auth.Application.Abstractions;

public interface ITokenService
{
    Task<IssuedToken> CreateTokenAsync(string username, IReadOnlyList<string> scopes, CancellationToken cancellationToken);

    Task<ValidateResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken);
}
