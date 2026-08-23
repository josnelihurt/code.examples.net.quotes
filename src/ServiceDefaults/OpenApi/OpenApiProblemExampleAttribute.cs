namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Declares a sample <c>application/problem+json</c> body for one response status on an MVC
/// action or controller. Multiple instances are allowed; the transformer picks the entry whose
/// <see cref="StatusCode"/> matches the declared OpenAPI response.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class OpenApiProblemExampleAttribute : Attribute
{
    public OpenApiProblemExampleAttribute(int statusCode) => StatusCode = statusCode;

    public int StatusCode { get; }

    public string? ErrorCode { get; init; }

    public string? Title { get; init; }

    public string? Detail { get; init; }

    public string? ValidationProperty { get; init; }

    public string? ValidationMessage { get; init; }

    internal OpenApiProblemExampleMetadata ToMetadata() => new()
    {
        StatusCode = StatusCode,
        ErrorCode = ErrorCode,
        Title = Title,
        Detail = Detail,
        ValidationProperty = ValidationProperty,
        ValidationMessage = ValidationMessage
    };
}
