using FluentValidation;

namespace Auth.Api;

public static class ValidationFilter
{
    public static async ValueTask<object?> ValidateAsync<T>(T? body, HttpContext http) where T : class
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
            return null;
        }

        var result = await validator.ValidateAsync(body);
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
