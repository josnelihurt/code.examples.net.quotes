using ErrorOr;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Colocated OpenAPI problem+json sample attached to an endpoint or route group. Read by
/// <see cref="OpenApiProblemExampleTransformer"/> at document generation time.
/// </summary>
internal sealed class OpenApiProblemExampleMetadata
{
    public required int StatusCode { get; init; }

    /// <summary>Domain/application error; messages come from the canonical Error source.</summary>
    public Error? Error { get; init; }

    public string? ErrorCode { get; init; }

    public string? Title { get; init; }

    public string? Detail { get; init; }

    /// <summary>Transport validation shape: property-keyed errors without errorCode.</summary>
    public string? ValidationProperty { get; init; }

    public string? ValidationMessage { get; init; }
}
