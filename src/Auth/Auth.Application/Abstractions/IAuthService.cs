namespace Auth.Application.Abstractions;

public interface IAuthService
{
    LoginResult? Login(LoginRequest request);
    ValidateResult Validate(string accessToken);
}
