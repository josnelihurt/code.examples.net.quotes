using ErrorOr;

namespace Auth.Application.Abstractions;

/// <summary>
/// Canonical auth errors raised by the application service. Codes surface as ProblemDetails
/// <c>errorCode</c> extensions, so renaming one is a breaking change. Transport-level codes
/// the endpoint layer owns itself (for example <c>auth.token_missing</c>, declared once in
/// ServiceDefaults' <c>JwtAuthExtensions</c>) do not live here.
/// </summary>
public static class AuthErrors
{
    public static Error InvalidCredentials =>
        Error.Unauthorized("auth.invalid_credentials", "Invalid credentials.");
}
