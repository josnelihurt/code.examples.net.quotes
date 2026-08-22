using AspireQuotesPoc.ServiceDefaults.Errors;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// The MVC counterpart of <see cref="ErrorOrHttpExtensions"/>. Controllers deal in
/// <see cref="ActionResult"/> rather than <see cref="IResult"/>, so they need their own
/// extension — but the payload comes from the same factory, which is what keeps the
/// controller-based and minimal-API versions of the API wire-compatible.
/// </summary>
public static class ErrorOrMvcExtensions
{
    /// <summary>
    /// Maps ErrorOr failures onto the seed's RFC 9457 error envelope for MVC controllers.
    /// Produces exactly the body <see cref="ErrorOrHttpExtensions.ToProblem(List{Error}, HttpContext)"/>
    /// would produce for the same errors.
    /// </summary>
    public static ActionResult ToActionResult(this List<Error> errors, HttpContext? httpContext = null) =>
        new ProblemDetailsActionResult(ProblemDetailsFactory.Create(errors, httpContext));

    /// <summary>Single-error convenience overload.</summary>
    public static ActionResult ToActionResult(this Error error, HttpContext? httpContext = null) =>
        new List<Error> { error }.ToActionResult(httpContext);
}
