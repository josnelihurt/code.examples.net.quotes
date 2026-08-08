using Auth.Domain;

namespace Auth.Infrastructure;

public sealed class HardcodedCredentialStore : ICredentialStore
{
    private const string ExpectedUsername = "jrb";
    private const string ExpectedPassword = "supersecret";

    public bool Validate(string username, string password) =>
        string.Equals(username, ExpectedUsername, StringComparison.Ordinal)
        && string.Equals(password, ExpectedPassword, StringComparison.Ordinal);
}
