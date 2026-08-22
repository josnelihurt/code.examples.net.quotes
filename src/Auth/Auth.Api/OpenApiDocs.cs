namespace Auth.Api;

/// <summary>
/// Narrative applied to the Auth API's OpenAPI document: the info description Scalar renders
/// at the top of the reference and the description for the <c>Auth</c> tag.
/// </summary>
internal static class OpenApiDocs
{
    internal const string Description = """
        Issues and introspects the JWT access tokens the Quotes API authorizes against.

        **Single supported version: v1** — served under `/api/v1/auth` and implemented with
        minimal APIs only. The MVC controller transport demonstrated by the Quotes API (v0)
        is intentionally not replicated here.

        Typical use:

        1. `POST /api/v1/auth/login` with username/password — the response carries the
           `accessToken` (development users: `jrb`/`supersecret` with both scopes,
           `reader`/`readsecret` read-only).
        2. Send `Authorization: Bearer {accessToken}` to the Quotes API (`/api/v1/quotes`).
        3. Optionally `POST /api/v1/auth/validate` to introspect a token; valid and invalid
           tokens both answer `200 { valid, username }`.

        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions.
        - Both endpoints are rate limited per client IP (fixed window, 10 requests / 30
          seconds by default); over-limit answers `429` with `auth.rate_limited`.
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    internal static readonly IReadOnlyDictionary<string, string> TagDescriptions =
        new Dictionary<string, string>
        {
            ["Auth"] = "Login and RFC 7662-style token introspection; rate limited per client IP.",
        };
}
