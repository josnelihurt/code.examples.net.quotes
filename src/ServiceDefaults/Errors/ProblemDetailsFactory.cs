using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace AspireQuotesPoc.ServiceDefaults.Errors;

/// <summary>
/// The single place where an <see cref="Error"/> becomes an RFC 9457 payload. Both transports
/// (minimal APIs via <c>ToProblem</c>, MVC controllers via <c>ToActionResult</c>) build their
/// response from this factory so the two API versions cannot drift apart on error shape.
/// </summary>
internal static class ProblemDetailsFactory
{
    internal const string ErrorCodeExtension = "errorCode";
    internal const string CorrelationIdExtension = "correlationId";
    internal const string ValidationTitle = "One or more validation errors occurred.";

    /// <summary>
    /// Builds the problem payload for <paramref name="errors"/>. Returns an
    /// <see cref="HttpValidationProblemDetails"/> when any error is a validation failure,
    /// otherwise a plain <see cref="ProblemDetails"/>.
    /// </summary>
    internal static ProblemDetails Create(List<Error> errors, HttpContext? httpContext)
    {
        var primary = Primary(errors);
        var extensions = Extensions(primary, httpContext);

        if (errors.Exists(e => e.Type is ErrorType.Validation))
        {
            var problem = new HttpValidationProblemDetails(ValidationErrors(errors))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = ValidationTitle,
                Type = TypeLink(StatusCodes.Status400BadRequest)
            };

            Merge(problem.Extensions, extensions);
            return problem;
        }

        var statusCode = StatusCode(primary);
        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = primary.Description,
            Type = TypeLink(statusCode)
        };

        Merge(details.Extensions, extensions);
        return details;
    }

    private static Error Primary(List<Error> errors) => errors.Count > 0
        ? errors[0]
        : Error.Unexpected("error.unknown", "An unexpected error occurred.");

    private static Dictionary<string, string[]> ValidationErrors(List<Error> errors) => errors
        .Where(e => e.Type is ErrorType.Validation)
        .GroupBy(e => e.Code)
        .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

    private static int StatusCode(Error primary) => primary.Type switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };

    private static Dictionary<string, object?> Extensions(Error error, HttpContext? httpContext)
    {
        var extensions = new Dictionary<string, object?> { [ErrorCodeExtension] = error.Code };
        if (httpContext is not null)
        {
            extensions[CorrelationIdExtension] = httpContext.GetCorrelationId();
        }

        return extensions;
    }

    /// <summary>
    /// The RFC 9110 status links ASP.NET Core would otherwise fill in. Set explicitly so the
    /// minimal-API and MVC pipelines emit the same <c>type</c> instead of relying on each
    /// pipeline's own defaulting.
    /// </summary>
    private static string TypeLink(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1"
    };

    private static void Merge(IDictionary<string, object?> target, Dictionary<string, object?> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }
}
