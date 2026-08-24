namespace Quotes.Api;

/// <summary>
/// The authorization vocabulary this API enforces, declared by the API that owns it —
/// ServiceDefaults registers policies parameterized, without context knowledge. The
/// Auth context mints these same values into tokens
/// (<c>Auth.Application.Abstractions.AuthorizationScopes</c>); the two spellings cannot
/// reference each other and are pinned together by <c>Architecture.Tests</c>, the one
/// test project allowed to see both contexts.
/// </summary>
public static class QuoteScopes
{
    public const string ReadPolicy = "quotes:read";
    public const string ReadScope = "quotes:read";
    public const string WritePolicy = "quotes:write";
    public const string WriteScope = "quotes:write";
}
