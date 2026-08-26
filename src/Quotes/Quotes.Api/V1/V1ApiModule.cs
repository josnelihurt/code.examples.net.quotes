using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Quotes.Api.ApiModules;
using Quotes.Api.V1.Endpoints;

namespace Quotes.Api.V1;

/// <summary>The minimal-API transport: the preferred integration surface.</summary>
internal sealed class V1ApiModule : IApiModule
{
    private const string _crossCutting = """
        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions.
        - Pagination is 1-based (`page` from 1, `pageSize` between 1 and 100, default 20).
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    public string? DocumentName => "v1";

    public OpenApiDocumentInfo? DocumentInfo => new(
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
        });

    public void AddServices(IServiceCollection services)
    {
        services.AddOpenApi("v1", options => options.ConfigureStandardOpenApi("v1"));
        // Minimal-API Data Annotations support (the v1 request DTOs carry them).
        services.AddValidation();
    }

    public void MapEndpoints(WebApplication app) => QuoteEndpoints.Map(app);
}
