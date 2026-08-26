using AspireQuotesPoc.ServiceDefaults.OpenApi;

namespace Quotes.Api.ApiModules;

/// <summary>
/// One API version, owning everything about itself: the OpenAPI document name it publishes,
/// its self-contained document narrative, its service registrations (including its literal
/// <c>AddOpenApi</c> call — the literal must stay in code or the XML-comment source
/// generator drops the documentation) and its endpoint mapping.
/// </summary>
/// <remarks>
/// Modules are found by reflection over the host assembly (see
/// <see cref="ApiModuleRegistry"/>), so adding a version means adding a folder with a
/// module — Program.cs stays agnostic of which versions exist. There is no registration
/// list to forget.
/// </remarks>
internal interface IApiModule
{
    /// <summary>
    /// The OpenAPI document name this module contributes to the Scalar picker and the
    /// per-document narrative registration, or <c>null</c> for a transport that publishes
    /// no runtime document at all.
    /// </summary>
    string? DocumentName { get; }

    /// <summary>The self-contained narrative for this module's document; null when it has none.</summary>
    OpenApiDocumentInfo? DocumentInfo { get; }

    /// <summary>Registers this version's services (its literal AddOpenApi call lives here).</summary>
    void AddServices(IServiceCollection services);

    /// <summary>Maps this version's endpoints onto the application.</summary>
    void MapEndpoints(WebApplication app);
}
