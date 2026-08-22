using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Auth.Domain.Abstractions;

namespace Auth.Infrastructure;

/// <summary>
/// Fixed single-user store for local scaffolding. Replacing this with a real
/// <see cref="ICredentialStore"/> is the only change needed to move off hard-coded credentials.
/// </summary>
public sealed class HardcodedCredentialStore : ICredentialStore
{
    [SuppressMessage(
        "Security",
        "S2068:Hard-coded credentials are security-sensitive",
        Justification = "Local scaffolding credential; there is no credential backing store to read from.")]
    private const string _expectedPassword = "supersecret";
    private const string _expectedUsername = "jrb";

    public Task<bool> ValidateAsync(string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Fixed-time comparison over SHA-256 digests: no early exit, so nothing about the
        // expected values (not even their lengths) leaks through response timing.
        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(username ?? string.Empty)),
            SHA256.HashData(Encoding.UTF8.GetBytes(_expectedUsername)));
        var passwordMatches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty)),
            SHA256.HashData(Encoding.UTF8.GetBytes(_expectedPassword)));

        return Task.FromResult(usernameMatches && passwordMatches);
    }
}
