using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Microsoft.Extensions.Hosting;

public static class ErrorOrHttpExtensions
{
    /// <summary>
    /// Maps ErrorOr failures onto the single RFC 9457 error envelope used across the seed.
    /// Validation errors aggregate into a 400 validation problem keyed by error code; every
    /// other error maps by <see cref="ErrorType"/>. The error code and correlation id travel
    /// as ProblemDetails extensions.
    /// </summary>
    public static IResult ToProblem(this List<Error> errors, HttpContext? httpContext = null)
    {
        var primary = errors.Count > 0
            ? errors[0]
            : Error.Unexpected("error.unknown", "An unexpected error occurred.");

        var extensions = Extensions(primary, httpContext);

        if (errors.Any(e => e.Type is ErrorType.Validation))
        {
            var validationErrors = errors
                .Where(e => e.Type is ErrorType.Validation)
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return Results.ValidationProblem(validationErrors, extensions: extensions);
        }

        var statusCode = primary.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(
            statusCode: statusCode,
            title: ReasonPhrases.GetReasonPhrase(statusCode),
            detail: primary.Description,
            extensions: extensions);
    }

    /// <summary>Single-error convenience overload.</summary>
    public static IResult ToProblem(this Error error, HttpContext? httpContext = null) =>
        new List<Error> { error }.ToProblem(httpContext);

    private static Dictionary<string, object?> Extensions(Error error, HttpContext? httpContext)
    {
        var extensions = new Dictionary<string, object?> { ["errorCode"] = error.Code };
        if (httpContext is not null)
        {
            extensions["correlationId"] = httpContext.GetCorrelationId();
        }

        return extensions;
    }
}
