using FluentValidation;

namespace Quotes.Api;

public static class ValidationFilter
{
    /// <summary>
    /// Returns a problem result when <paramref name="body"/> is missing or invalid, otherwise null.
    /// </summary>
    public static async ValueTask<IResult?> ValidateAsync<T>(T? body, HttpContext http) where T : class
    {
        if (body is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Request body is required."]
            });
        }

        var validator = http.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(ValidationFilter).FullName!)
                .LogWarning("No validator registered for {RequestType}; skipping validation", typeof(T).Name);
            return null;
        }

        var result = await validator.ValidateAsync(body, http.RequestAborted);
        if (result.IsValid)
        {
            return null;
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }
}
