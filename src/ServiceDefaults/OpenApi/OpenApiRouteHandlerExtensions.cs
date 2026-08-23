using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Colocates OpenAPI problem+json samples on minimal-API routes and route groups.
/// </summary>
public static class OpenApiRouteHandlerExtensions
{
    /// <summary>Documents a domain/application error sample via the canonical <see cref="Error"/>.</summary>
    public static RouteHandlerBuilder WithProblemExample(
        this RouteHandlerBuilder builder,
        int statusCode,
        Error error) =>
        builder.WithMetadata(new OpenApiProblemExampleMetadata
        {
            StatusCode = statusCode,
            Error = error
        });

    /// <summary>Documents a domain/application error sample by public error code and message.</summary>
    public static RouteHandlerBuilder WithProblemExample(
        this RouteHandlerBuilder builder,
        int statusCode,
        string errorCode,
        string detail) =>
        builder.WithMetadata(new OpenApiProblemExampleMetadata
        {
            StatusCode = statusCode,
            ErrorCode = errorCode,
            Detail = detail
        });

    /// <summary>Documents an infrastructure error sample (JWT 401, scope 403, rate limit 429).</summary>
    public static RouteHandlerBuilder WithProblemExample(
        this RouteHandlerBuilder builder,
        int statusCode,
        string title,
        string detail,
        string? errorCode = null) =>
        builder.WithMetadata(new OpenApiProblemExampleMetadata
        {
            StatusCode = statusCode,
            Title = title,
            Detail = detail,
            ErrorCode = errorCode
        });

    /// <summary>
    /// Documents transport validation (property-keyed <c>errors</c>, no <c>errorCode</c> in the body).
    /// </summary>
    public static RouteHandlerBuilder WithValidationProblemExample(
        this RouteHandlerBuilder builder,
        string propertyName,
        string message) =>
        builder.WithMetadata(new OpenApiProblemExampleMetadata
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ValidationProperty = propertyName,
            ValidationMessage = message
        });

    /// <summary>Applies an infrastructure error sample to every route in the group.</summary>
    public static RouteGroupBuilder WithProblemExample(
        this RouteGroupBuilder group,
        int statusCode,
        string title,
        string detail,
        string? errorCode = null) =>
        group.WithMetadata(new OpenApiProblemExampleMetadata
        {
            StatusCode = statusCode,
            Title = title,
            Detail = detail,
            ErrorCode = errorCode
        });
}
