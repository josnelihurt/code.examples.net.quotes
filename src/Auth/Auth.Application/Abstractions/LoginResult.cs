namespace Auth.Application.Abstractions;

public sealed record LoginResult(string AccessToken, string Username, int ExpiresIn);
