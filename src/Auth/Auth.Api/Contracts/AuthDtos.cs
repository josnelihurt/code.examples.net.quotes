namespace Auth.Api.Contracts;

public sealed class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponseDto
{
    public required string AccessToken { get; init; }
    public required string CorrelationId { get; init; }
    public required int ExpiresIn { get; init; }
    public required string Username { get; init; }
}

public sealed class ErrorResponseDto
{
    public required string Error { get; init; }
}

public sealed class ValidateRequestDto
{
    public string? AccessToken { get; set; }
}

public sealed class ValidateResponseDto
{
    public required bool Valid { get; init; }
    public string? Username { get; init; }
}
