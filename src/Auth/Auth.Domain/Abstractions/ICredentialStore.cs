namespace Auth.Domain.Abstractions;

public interface ICredentialStore
{
    /// <summary>Checks credentials asynchronously; hashing or remote stores must not block callers.</summary>
    Task<bool> ValidateAsync(string username, string password, CancellationToken cancellationToken);
}
