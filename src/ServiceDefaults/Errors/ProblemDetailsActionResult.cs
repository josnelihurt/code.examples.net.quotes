using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AspireQuotesPoc.ServiceDefaults.Errors;

/// <summary>
/// Writes a problem payload from an MVC action through <see cref="IProblemDetailsService"/> —
/// the same writer minimal-API endpoints go through.
/// </summary>
/// <remarks>
/// Returning a plain <see cref="ObjectResult"/> instead would serialize via MVC's output
/// formatters, which answer <c>application/json</c> and skip the <c>traceId</c> the shared writer
/// attaches. Going through the service is what makes a controller response byte-identical to the
/// minimal-API response for the same error.
/// </remarks>
public sealed class ProblemDetailsActionResult(ProblemDetails problemDetails) : ActionResult
{
    /// <summary>The payload this result writes. Exposed so service tests can assert on it.</summary>
    public ProblemDetails ProblemDetails { get; } = problemDetails;

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        var statusCode = ProblemDetails.Status ?? StatusCodes.Status500InternalServerError;
        http.Response.StatusCode = statusCode;

        var service = http.RequestServices?.GetService<IProblemDetailsService>();
        if (service is not null)
        {
            var written = await service.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = ProblemDetails
            });

            if (written)
            {
                return;
            }
        }

        // No ProblemDetails service registered: still answer with the right media type.
        // WriteAsJsonAsync resets Content-Type, so it has to be passed in rather than preset.
        await http.Response.WriteAsJsonAsync(
            ProblemDetails,
            ProblemDetails.GetType(),
            options: null,
            contentType: "application/problem+json");
    }
}
