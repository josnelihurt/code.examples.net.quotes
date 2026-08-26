using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Quotes.Api.ApiModules;
using Quotes.Api.V2.OpenApi;
using Quotes.Api.V2.Services;

namespace Quotes.Api.V2;

/// <summary>
/// The proto-first transport: a contract-first .proto with google.api.http annotations,
/// served through the generated-service adapter with a wire identical to v0/v1.
/// </summary>
internal sealed class V2ApiModule : IApiModule
{
    private const string _crossCutting = """
        Cross-cutting behavior:

        - Every error response is RFC 9457 `application/problem+json` with `errorCode` and
          `correlationId` extensions.
        - Pagination is 1-based (`page` from 1, `pageSize` between 1 and 100, default 20).
        - Send `X-Correlation-Id` to correlate calls; it is echoed on every response.
        """;

    public string? DocumentName => "v2";

    public OpenApiDocumentInfo? DocumentInfo => new(
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
        });

    public void AddServices(IServiceCollection services)
    {
        // The v2 document's schemas come from the proto descriptors, not CLR reflection.
        services.AddOpenApi("v2", options =>
        {
            options.ConfigureStandardOpenApi("v2");
            options.AddSchemaTransformer<ProtoSchemaTransformer>();
        });

        // The adapter invokes the generated service in-process; it resolves the same
        // decorated use cases from the same container as every other module.
        services.AddScoped<QuoteGrpcService>();
    }

    public void MapEndpoints(WebApplication app) => Endpoints.QuoteEndpoints.Map(app);
}
