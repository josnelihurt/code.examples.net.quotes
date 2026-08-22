using ErrorOr;

namespace Auth.Application.Abstractions;

public interface IAuthService
{
    Task<ErrorOr<LoginResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    ValidateResult Validate(string accessToken);
}
