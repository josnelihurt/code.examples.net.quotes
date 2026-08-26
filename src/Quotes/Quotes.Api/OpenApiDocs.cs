using AspireQuotesPoc.ServiceDefaults.OpenApi;

namespace Quotes.Api;

/// <summary>
/// The OpenAPI narrative for each Quotes document. Every entry describes its own version
/// only — paths, transport and behavior — so adding a transport never edits the frozen
/// contracts of the versions beside it. Cross-version comparisons (parity, drift) live in
/// the repository's documentation pages, not in the machine contracts.
/// </summary>
internal static class OpenApiDocs
{
    private const string _crossCutting = """
        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions.
        - Pagination is 1-based (`page` from 1, `pageSize` between 1 and 100, default 20).
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    internal static readonly IReadOnlyDictionary<string, OpenApiDocumentInfo> Documents =
        new Dictionary<string, OpenApiDocumentInfo>
        {
            ["v0"] = new OpenApiDocumentInfo(
                Description: $$"""
                    Quote catalog for the Aspire Quotes platform, served by ASP.NET MVC
                    controllers.

                    Typical use:

                    1. Obtain a bearer JWT from the Auth API: `POST /api/v1/auth/login`
                       (development users: see the repository's dev-credentials documentation;
                       the maintainer holds both scopes, the reader is read-only).
                    2. Send `Authorization: Bearer {accessToken}` to every operation below — reads
                       require the `quotes:read` scope claim, create requires `quotes:write`; a valid
                       token without the scope answers 403.
                    3. The catalog boots seeded (eight quotes), so reads serve data from the first
                       call: browse with `GET /api/v0/quotes`, `GET /api/v0/quotes/{id}` or
                       `GET /api/v0/quotes/random`, then add your own with `POST /api/v0/quotes`.

                    {{_crossCutting}}
                    """,
                TagDescriptions: new Dictionary<string, string>
                {
                    ["Quotes v0"] = "The quote catalog served by MVC controllers.",
                }),
            ["v1"] = new OpenApiDocumentInfo(
                Description: $$"""
                    Quote catalog for the Aspire Quotes platform, served by minimal APIs —
                    the preferred integration surface.

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

                    {{_crossCutting}}
                    """,
                TagDescriptions: new Dictionary<string, string>
                {
                    ["Quotes v1"] = "The quote catalog served by minimal APIs.",
                }),
            ["v2"] = new OpenApiDocumentInfo(
                Description: $$"""
                    Quote catalog for the Aspire Quotes platform, defined contract-first: a
                    proto file with `google.api.http` annotations drives the messages and the
                    service skeleton this transport serves.

                    Typical use:

                    1. Obtain a bearer JWT from the Auth API: `POST /api/v1/auth/login`
                       (development users: see the repository's dev-credentials documentation;
                       the maintainer holds both scopes, the reader is read-only).
                    2. Send `Authorization: Bearer {accessToken}` to every operation below — reads
                       require the `quotes:read` scope claim, create requires `quotes:write`; a valid
                       token without the scope answers 403.
                    3. The catalog boots seeded (eight quotes), so reads serve data from the first
                       call: browse with `GET /api/v2/quotes`, `GET /api/v2/quotes/{id}` or
                       `GET /api/v2/quotes/random`, then add your own with `POST /api/v2/quotes`.

                    {{_crossCutting}}
                    """,
                TagDescriptions: new Dictionary<string, string>
                {
                    ["Quotes v2"] = "The quote catalog defined by a contract-first .proto with google.api.http annotations.",
                }),
        };
}
