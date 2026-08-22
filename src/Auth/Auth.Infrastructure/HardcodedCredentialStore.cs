using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;

namespace Auth.Infrastructure;

/// <summary>
/// Fixed two-user store for local scaffolding: the maintainer holds read+write scopes,
/// the reader holds read-only, so least-privilege tokens exist from day one. Replacing
/// this with a real <see cref="ICredentialStore"/> is the only change needed to move
/// off hard-coded credentials.
/// </summary>
public sealed class HardcodedCredentialStore : ICredentialStore
{
    [SuppressMessage(
        "Security",
        "S2068:Hard-coded credentials are security-sensitive",
        Justification = "Local scaffolding credentials; there is no credential backing store to read from.")]
    private static readonly (string Username, string Password, string[] Scopes)[] _users =
    [
        ("jrb", "supersecret", [AuthorizationScopes.QuotesRead, AuthorizationScopes.QuotesWrite]),
        ("reader", "readsecret", [AuthorizationScopes.QuotesRead])
    ];

    public Task<CredentialValidationResult> ValidateAsync(string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var usernameDigest = SHA256.HashData(Encoding.UTF8.GetBytes(username ?? string.Empty));
        var passwordDigest = SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty));

        // Fixed-time comparison over SHA-256 digests: no early exit, so nothing about the
        // expected values (not even their lengths) leaks through response timing.
        foreach (var (expectedUsername, expectedPassword, scopes) in _users)
        {
            var usernameMatches = CryptographicOperations.FixedTimeEquals(
                usernameDigest,
                SHA256.HashData(Encoding.UTF8.GetBytes(expectedUsername)));
            var passwordMatches = CryptographicOperations.FixedTimeEquals(
                passwordDigest,
                SHA256.HashData(Encoding.UTF8.GetBytes(expectedPassword)));

            if (usernameMatches && passwordMatches)
            {
                return Task.FromResult(new CredentialValidationResult(true, scopes));
            }
        }

        return Task.FromResult(CredentialValidationResult.Invalid);
    }
}
