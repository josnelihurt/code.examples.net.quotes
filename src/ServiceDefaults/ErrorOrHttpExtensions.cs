using AspireQuotesPoc.ServiceDefaults.Errors;
using ErrorOr;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

public static class ErrorOrHttpExtensions
{
    /// <summary>
    /// Maps ErrorOr failures onto the single RFC 9457 error envelope used across the seed, for
    /// minimal-API endpoints. Validation errors aggregate into a 400 validation problem keyed by
    /// error code; every other error maps by <see cref="ErrorType"/>. The error code and
    /// correlation id travel as ProblemDetails extensions.
    /// </summary>
    /// <remarks>
    /// The payload itself is built by <see cref="ProblemDetailsFactory"/>, shared with the MVC
    /// <c>ToActionResult</c> counterpart so both API versions answer failures identically.
    /// </remarks>
    public static IResult ToProblem(this List<Error> errors, HttpContext? httpContext = null) =>
        Results.Problem(ProblemDetailsFactory.Create(errors, httpContext));

    /// <summary>Single-error convenience overload.</summary>
    public static IResult ToProblem(this Error error, HttpContext? httpContext = null) =>
        new List<Error> { error }.ToProblem(httpContext);
}
