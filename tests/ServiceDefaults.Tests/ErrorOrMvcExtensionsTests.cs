using AspireQuotesPoc.ServiceDefaults.Errors;
using AspireQuotesPoc.ServiceDefaults.Http;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults.Tests;

/// <summary>
/// The MVC error path exists so controller-based API versions can answer failures. These tests
/// pin it to the minimal-API path: if the two ever diverge, the two API versions stop being
/// interchangeable and the parity guarantee the seed advertises is broken.
/// </summary>
public class ErrorOrMvcExtensionsTests
{
    private static DefaultHttpContext CreateContext()
    {
        var http = new DefaultHttpContext();
        http.Items[HttpHeaderNames.CorrelationId] = "corr-42";
        return http;
    }

    /// <summary>Errors are not xUnit-serializable, so theories address them by name.</summary>
    private static List<Error> ErrorsNamed(string name) => name switch
    {
        "notFound" => [Error.NotFound("quote.not_found", "Quote not found.")],
        "conflict" => [Error.Conflict("quote.duplicate_fingerprint", "Duplicate.")],
        "unauthorized" => [Error.Unauthorized("auth.invalid_credentials", "Invalid credentials.")],
        "forbidden" => [Error.Forbidden("auth.forbidden", "Insufficient scope.")],
        "unexpected" => [Error.Unexpected("error.boom", "Something broke.")],
        "validation" =>
        [
            Error.Validation("quote.text_too_short", "Quote text must be at least 12 characters."),
            Error.Validation("quote.text_needs_more_words", "Quote text must contain at least 3 words.")
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown error fixture.")
    };

    private static ProblemDetails MinimalApiProblem(List<Error> errors, HttpContext http) =>
        errors.ToProblem(http).ShouldBeOfType<ProblemHttpResult>().ProblemDetails;

    private static ProblemDetails MvcProblem(List<Error> errors, HttpContext http) =>
        errors.ToActionResult(http).ShouldBeOfType<ProblemDetailsActionResult>().ProblemDetails;

    [Theory]
    [InlineData("notFound", StatusCodes.Status404NotFound)]
    [InlineData("conflict", StatusCodes.Status409Conflict)]
    [InlineData("unauthorized", StatusCodes.Status401Unauthorized)]
    [InlineData("forbidden", StatusCodes.Status403Forbidden)]
    [InlineData("unexpected", StatusCodes.Status500InternalServerError)]
    [InlineData("validation", StatusCodes.Status400BadRequest)]
    public void An_error_maps_to_the_same_status_as_the_minimal_api_path(string name, int expectedStatus)
    {
        var http = CreateContext();

        var mvc = MvcProblem(ErrorsNamed(name), http);

        mvc.Status.ShouldBe(expectedStatus);
        MinimalApiProblem(ErrorsNamed(name), http).Status.ShouldBe(expectedStatus);
    }

    [Theory]
    [InlineData("notFound")]
    [InlineData("conflict")]
    [InlineData("unauthorized")]
    [InlineData("forbidden")]
    [InlineData("unexpected")]
    [InlineData("validation")]
    public void An_error_produces_the_same_envelope_on_both_transports(string name)
    {
        var http = CreateContext();

        var minimal = MinimalApiProblem(ErrorsNamed(name), http);
        var mvc = MvcProblem(ErrorsNamed(name), http);

        mvc.Status.ShouldBe(minimal.Status);
        mvc.Title.ShouldBe(minimal.Title);
        mvc.Detail.ShouldBe(minimal.Detail);
        mvc.Type.ShouldBe(minimal.Type);
        mvc.Extensions["errorCode"].ShouldBe(minimal.Extensions["errorCode"]);
        mvc.Extensions["correlationId"].ShouldBe(minimal.Extensions["correlationId"]);
    }

    [Fact]
    public void Validation_errors_are_keyed_by_error_code_on_both_transports()
    {
        var http = CreateContext();

        var minimal = MinimalApiProblem(ErrorsNamed("validation"), http)
            .ShouldBeOfType<HttpValidationProblemDetails>();
        var mvc = MvcProblem(ErrorsNamed("validation"), http)
            .ShouldBeOfType<HttpValidationProblemDetails>();

        mvc.Errors.Keys.ShouldBe(minimal.Errors.Keys, ignoreOrder: true);
        mvc.Errors["quote.text_too_short"].ShouldBe(minimal.Errors["quote.text_too_short"]);
        mvc.Errors["quote.text_needs_more_words"].ShouldBe(minimal.Errors["quote.text_needs_more_words"]);
    }

    [Fact]
    public async Task A_problem_is_served_as_application_problem_json()
    {
        // No IProblemDetailsService is registered here, which exercises the fallback branch;
        // the wired-up path is covered end to end by the API's version parity suite.
        var http = CreateContext();
        http.Response.Body = new MemoryStream();
        var context = new ActionContext { HttpContext = http, RouteData = new(), ActionDescriptor = new() };

        await ErrorsNamed("notFound").ToActionResult(http).ExecuteResultAsync(context);

        http.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        http.Response.ContentType.ShouldStartWith("application/problem+json");
    }

    [Fact]
    public void A_single_error_overload_behaves_like_the_list()
    {
        var problem = Error.NotFound("quote.not_found", "Quote not found.")
            .ToActionResult()
            .ShouldBeOfType<ProblemDetailsActionResult>()
            .ProblemDetails;
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Extensions["errorCode"].ShouldBe("quote.not_found");
        problem.Extensions.ShouldNotContainKey("correlationId");
    }
}
