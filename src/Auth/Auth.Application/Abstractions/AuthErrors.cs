using ErrorOr;

namespace Auth.Application.Abstractions;

/// <summary>
/// Canonical auth errors. Codes surface as ProblemDetails <c>errorCode</c> extensions,
/// so renaming one is a breaking change.
/// </summary>
public static class AuthErrors
{
    public static Error InvalidCredentials =>
        Error.Unauthorized("auth.invalid_credentials", "Invalid credentials.");

    public static Error MissingToken =>
        Error.Validation("auth.token_missing", "An access token is required.");
}
