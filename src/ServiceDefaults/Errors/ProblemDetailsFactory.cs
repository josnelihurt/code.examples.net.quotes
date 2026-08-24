using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
                Type = ProblemDetailsBuilder.TypeLink(StatusCodes.Status400BadRequest)
            };

            Merge(problem.Extensions, extensions);
            return problem;
        }

        var statusCode = StatusCode(primary);
        return ProblemDetailsBuilder.Build(statusCode, primary.Code, primary.Description, httpContext);
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
    /// The RFC 9110 type links and status phrases live in <see cref="ProblemDetailsBuilder"/>
    /// so middleware-produced problems share them.
    /// </summary>
    private static void Merge(IDictionary<string, object?> target, Dictionary<string, object?> source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }
}
