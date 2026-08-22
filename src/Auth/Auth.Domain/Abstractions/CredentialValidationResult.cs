namespace Auth.Domain.Abstractions;

/// <summary>
/// Outcome of a credential check. A valid decision carries the scopes granted to the
/// principal, so the caller never has to infer authorization from the username.
/// </summary>
public sealed record CredentialValidationResult(bool IsValid, IReadOnlyList<string> Scopes)
{
    public static CredentialValidationResult Invalid { get; } = new(false, []);
}
