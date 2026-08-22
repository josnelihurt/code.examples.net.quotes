using ErrorOr;

namespace Auth.Application.Abstractions;

public interface IAuthService
{
    Task<ErrorOr<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// RFC 7662-style introspection: the answer (valid or not) is data, not an error —
    /// an invalid token returns <c>Valid = false</c> rather than an error result.
    /// </summary>
    Task<ValidateResult> ValidateAsync(string accessToken, CancellationToken cancellationToken);
}
