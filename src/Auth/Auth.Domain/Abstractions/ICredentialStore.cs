namespace Auth.Domain.Abstractions;

public interface ICredentialStore
{
    /// <summary>Checks credentials asynchronously and, on success, returns the granted scopes; hashing or remote stores must not block callers.</summary>
    Task<CredentialValidationResult> ValidateAsync(string username, string password, CancellationToken cancellationToken);
}
