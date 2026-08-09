namespace Auth.Domain;

public interface ICredentialStore
{
    bool Validate(string username, string password);
}
