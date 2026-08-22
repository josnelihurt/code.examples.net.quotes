namespace Auth.Application.Abstractions;

public sealed record ValidateResult(bool Valid, string? Username);
