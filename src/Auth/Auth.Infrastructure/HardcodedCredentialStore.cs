using System.Diagnostics.CodeAnalysis;
using Auth.Domain.Abstractions;

namespace Auth.Infrastructure;

/// <summary>
/// Fixed single-user store. The POC deliberately has no user database; replacing this with a real
/// <see cref="ICredentialStore"/> is the only change needed to move off hard-coded credentials.
/// </summary>
public sealed class HardcodedCredentialStore : ICredentialStore
{
    [SuppressMessage(
        "Security",
        "S2068:Hard-coded credentials are security-sensitive",
        Justification = "POC demo credential; there is no credential backing store to read from.")]
    private const string _expectedPassword = "supersecret";
    private const string _expectedUsername = "jrb";

    public bool Validate(string username, string password) =>
        string.Equals(username, _expectedUsername, StringComparison.Ordinal)
        && string.Equals(password, _expectedPassword, StringComparison.Ordinal);
}
