namespace Auth.Application.Abstractions;

public interface ITokenService
{
    string CreateToken(string username, out int expiresInSeconds);
    ValidateResult ValidateToken(string accessToken);
}
