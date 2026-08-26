using Quotes.Api.ApiModules;
using Quotes.Api.V3.OpenApi;
using Quotes.Api.V3.Services;

namespace Quotes.Api.V3;

/// <summary>
/// The stock transcoding transport: the google.api.http rules in the contract drive the
/// routing. The OpenAPI document is generated from the proto by the freeze pipeline (the
/// runtime cannot produce one), so this module registers no document narrative — it serves
/// the artifact instead.
/// </summary>
internal sealed class V3ApiModule : IApiModule
{
    public string? DocumentName => "v3";

    public AspireQuotesPoc.ServiceDefaults.OpenApi.OpenApiDocumentInfo? DocumentInfo => null;

    public void AddServices(IServiceCollection services) =>
        services.AddGrpc().AddJsonTranscoding();

    public void MapEndpoints(WebApplication app)
    {
        app.MapGrpcService<QuoteGrpcService>();
        // The document is generated from the proto by the freeze pipeline, not by the
        // runtime; it is itself a document, not an operation, so it stays out of the
        // generated documents.
        app.MapGet("/openapi/v3.json", () => Results.Content(V3OpenApiDocument.Json, "application/json"))
            .ExcludeFromDescription();
    }
}
