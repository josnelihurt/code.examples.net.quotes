namespace Auth.Domain;

public sealed class User
{
    public required string Username { get; init; }
}

public interface ICredentialStore
{
    bool Validate(string username, string password);
}
