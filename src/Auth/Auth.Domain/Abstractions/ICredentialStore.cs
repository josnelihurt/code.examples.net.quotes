namespace Auth.Domain.Abstractions;

public interface ICredentialStore
{
    bool Validate(string username, string password);
}
