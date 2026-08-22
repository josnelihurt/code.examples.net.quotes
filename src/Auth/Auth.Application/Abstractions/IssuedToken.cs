namespace Auth.Application.Abstractions;

/// <summary>A freshly minted token together with its configured lifetime.</summary>
public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);
