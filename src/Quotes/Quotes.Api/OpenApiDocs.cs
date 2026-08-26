namespace Quotes.Api;

/// <summary>
/// Narrative applied to the Quotes API's OpenAPI documents: the info description Scalar
/// renders at the top of the reference and the descriptions for the transport tags.
/// </summary>
internal static class OpenApiDocs
{
    internal const string Description = """
        Quote catalog for the Aspire Quotes platform.

        **Three JSON documents, four transports**: `v0` (MVC controllers), `v1` (minimal
        APIs) and `v2` (a proto contract served through an adapter) publish the same
        operations, payloads and error envelope; parity is enforced by tests. `v3` serves
        the same shape through stock gRPC-JSON transcoding and deliberately drifts (gRPC
        status error bodies, 200 on create, no OpenAPI document) — its contract of record is
        the proto file under `src/Quotes/Quotes.Api/V3/Contracts`. New integrations should
        prefer `v1`.

        Typical use:

        1. Obtain a bearer JWT from the Auth API: `POST /api/v1/auth/login`
           (development users: see the repository's dev-credentials documentation;
           the maintainer holds both scopes, the reader is read-only).
        2. Send `Authorization: Bearer {accessToken}` to every operation below — reads
           require the `quotes:read` scope claim, create requires `quotes:write`; a valid
           token without the scope answers 403.
        3. The catalog boots seeded (eight quotes), so reads serve data from the first
           call: browse with `GET /api/v1/quotes`, `GET /api/v1/quotes/{id}` or
           `GET /api/v1/quotes/random`, then add your own with `POST /api/v1/quotes`.
           The same operations exist under `/api/v0/quotes`, `/api/v2/quotes` and
           `/api/v3/quotes`.

        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions (v3 excepted: transcoding answers with the gRPC status
          envelope).
        - Pagination is 1-based (`page` from 1, `pageSize` between 1 and 100, default 20).
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    internal static readonly IReadOnlyDictionary<string, string> TagDescriptions =
        new Dictionary<string, string>
        {
            ["Quotes v0"] = "Controller transport of the quote catalog; same contract as v1, kept to demonstrate the transport swap.",
            ["Quotes v1"] = "Minimal-API transport of the quote catalog; the preferred integration surface.",
            ["Quotes v2"] = "Proto-first transport: a contract-first .proto with google.api.http annotations, served byte-identical to v0/v1 through a generated-service adapter.",
        };
}
