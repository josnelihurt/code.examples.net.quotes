using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Quotes.Api.ApiModules;

namespace Quotes.Api.V0;

/// <summary>The controller transport: MVC endpoints, their document and narrative.</summary>
internal sealed class V0ApiModule : IApiModule
{
    private const string _crossCutting = """
        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions.
        - Pagination is 1-based (`page` from 1, `pageSize` between 1 and 100, default 20).
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    public string? DocumentName => "v0";

    public OpenApiDocumentInfo? DocumentInfo => new(
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
        });

    public void AddServices(IServiceCollection services)
    {
        services.AddOpenApi("v0", options => options.ConfigureStandardOpenApi("v0"));
        services.AddStandardControllers();
    }

    public void MapEndpoints(WebApplication app) => app.MapControllers();
}
