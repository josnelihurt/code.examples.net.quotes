using AspireQuotesPoc.ServiceDefaults.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class MvcApiExtensions
{
    /// <summary>
    /// Registers controllers with the seed's error envelope applied to model-state failures.
    /// </summary>
    /// <remarks>
    /// <c>[ApiController]</c> short-circuits an invalid model before the action runs and writes its
    /// own payload through MVC's ProblemDetailsFactory, which decorates the body with a
    /// <c>traceId</c> the minimal-API validation filter never emits. Left alone that makes a
    /// controller version answer a malformed request differently from a minimal-API version of the
    /// same endpoint, so the response is rebuilt here to match.
    /// </remarks>
    public static IMvcBuilder AddStandardControllers(this IServiceCollection services)
    {
        // PostConfigure, not Configure: MVC's own ApiBehaviorOptionsSetup assigns
        // InvalidModelStateResponseFactory and would overwrite anything registered before it.
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                var problem = new HttpValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                };

                return new ProblemDetailsActionResult(problem);
            };
        });

        return services.AddControllers();
    }
}
