using System.Diagnostics.CodeAnalysis;
using Auth.Domain;

namespace Auth.Infrastructure;

/// <summary>
/// Fixed single-user store. The POC deliberately has no user database; replacing this with a real
/// <see cref="ICredentialStore"/> is the only change needed to move off hard-coded credentials.
/// </summary>
public sealed class HardcodedCredentialStore : ICredentialStore
{
    private const string ExpectedUsername = "jrb";

    [SuppressMessage(
        "Security",
        "S2068:Hard-coded credentials are security-sensitive",
        Justification = "POC demo credential; there is no credential backing store to read from.")]
    private const string ExpectedPassword = "supersecret";

    public bool Validate(string username, string password) =>
        string.Equals(username, ExpectedUsername, StringComparison.Ordinal)
        && string.Equals(password, ExpectedPassword, StringComparison.Ordinal);
}
