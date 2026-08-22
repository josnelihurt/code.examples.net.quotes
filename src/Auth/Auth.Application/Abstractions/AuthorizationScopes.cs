namespace Auth.Application.Abstractions;

/// <summary>
/// Scope vocabulary minted into tokens. ServiceDefaults' <c>JwtAuthExtensions</c> defines
/// the policies that consume these values in the resource APIs; a drift test pins the two
/// sides together because the platform kit cannot reference a service's application layer.
/// </summary>
public static class AuthorizationScopes
{
    public const string ClaimType = "scope";
    public const string QuotesRead = "quotes:read";
    public const string QuotesWrite = "quotes:write";
}
