using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace AspireQuotesPoc.ServiceDefaults.Errors;

/// <summary>
/// The public face of the seed's error envelope for failures that do not originate as an
/// ErrorOr result: middleware-produced problems (the 401 challenge, the 429 rate-limit
/// rejection) and any host-specific body. Everything built here carries the same
/// <c>errorCode</c>/<c>correlationId</c> extensions and RFC 9110 type links as
/// <see cref="ProblemDetailsFactory"/>, so clients parse exactly one error shape.
/// </summary>
public static class ProblemDetailsBuilder
{
    /// <summary>
    /// errorCode carried by transport-level validation failures (Data Annotations, model
    /// binding), whose <c>errors</c> dictionary is keyed by property name rather than by
    /// error code.
    /// </summary>
    public const string RequestValidationErrorCode = "validation.request_invalid";

    /// <summary>Builds a problem payload for a status, errorCode and detail.</summary>
    public static ProblemDetails Build(int statusCode, string errorCode, string detail, HttpContext? httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail,
            Type = TypeLink(statusCode)
        };

        problem.Extensions[ProblemDetailsFactory.ErrorCodeExtension] = errorCode;
        if (httpContext is not null)
        {
            problem.Extensions[ProblemDetailsFactory.CorrelationIdExtension] = httpContext.GetCorrelationId();
        }

        return problem;
    }

    /// <summary>
    /// The RFC 9110 status links ASP.NET Core would otherwise fill in. Set explicitly so the
    /// minimal-API and MVC pipelines emit the same <c>type</c> instead of relying on each
    /// pipeline's own defaulting.
    /// </summary>
    internal static string TypeLink(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status429TooManyRequests => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
        StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1"
    };
}
