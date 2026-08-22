using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace AspireQuotesPoc.ServiceDefaults.Validation;

/// <summary>
/// Endpoint filter that runs the registered FluentValidation validator for the request body.
/// Fails closed: the filter requires an <see cref="IValidator{T}"/> from DI, so a missing
/// registration throws when the filter is resolved instead of silently skipping validation.
/// Apply with <c>.AddEndpointFilter&lt;ValidationEndpointFilter&lt;TRequest&gt;&gt;()</c>.
/// </summary>
public sealed class ValidationEndpointFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : class
{
    private const string _bodyKey = "$body";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } body)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [_bodyKey] = ["Request body is required."]
            });
        }

        var result = await validator.ValidateAsync(body, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}
