namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Per-host narrative applied to every OpenAPI document by
/// <see cref="DocumentInfoTransformer"/>: the document description Scalar renders at the top
/// of the reference (typical use cases, auth flow, error model) and the descriptions for the
/// tags that group the operations. Hosts that register no instance keep the framework defaults.
/// </summary>
public sealed record OpenApiDocumentInfo(
    string? Description = null,
    IReadOnlyDictionary<string, string>? TagDescriptions = null);
