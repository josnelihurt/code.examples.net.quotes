namespace Auth.Application;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResult(string AccessToken, string Username, int ExpiresIn);

public sealed record ValidateResult(bool Valid, string? Username);

public interface ITokenService
{
    string CreateToken(string username, out int expiresInSeconds);
    ValidateResult ValidateToken(string accessToken);
}

public interface IAuthService
{
    LoginResult? Login(LoginRequest request);
    ValidateResult Validate(string accessToken);
}
