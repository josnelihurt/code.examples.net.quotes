namespace Quotes.Api;

/// <summary>
/// Narrative applied to the Quotes API's OpenAPI documents: the info description Scalar
/// renders at the top of the reference and the descriptions for the transport tags.
/// </summary>
internal static class OpenApiDocs
{
    internal const string Description = """
        Quote catalog for the Aspire Quotes platform.

        **Two transports, one contract**: `v0` (MVC controllers) and `v1` (minimal APIs)
        publish the same operations, payloads and error envelope; parity is enforced by
        tests. `v0` exists to demonstrate that transport style is a swappable detail of the
        architecture — new integrations should prefer `v1`.

        Typical use:

        1. Obtain a bearer JWT from the Auth API: `POST /api/v1/auth/login` (development
           users: `jrb`/`supersecret` with both scopes, `reader`/`readsecret` read-only).
        2. Send `Authorization: Bearer {accessToken}` to every operation below — reads
           require the `quotes:read` scope claim, create requires `quotes:write`; a valid
           token without the scope answers 403.
        3. The catalog boots seeded (eight quotes), so reads serve data from the first
           call: browse with `GET /api/v1/quotes`, `GET /api/v1/quotes/{id}` or
           `GET /api/v1/quotes/random`, then add your own with `POST /api/v1/quotes`.
           The same operations exist under `/api/v0/quotes`.

        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions.
        - Pagination is 1-based (`page` from 1, `pageSize` between 1 and 100, default 20).
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    internal static readonly IReadOnlyDictionary<string, string> TagDescriptions =
        new Dictionary<string, string>
        {
            ["Quotes v0"] = "Controller transport of the quote catalog; same contract as v1, kept to demonstrate the transport swap.",
            ["Quotes v1"] = "Minimal-API transport of the quote catalog; the preferred integration surface.",
        };
}
