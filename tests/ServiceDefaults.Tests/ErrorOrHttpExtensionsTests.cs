using AspireQuotesPoc.ServiceDefaults.Http;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults.Tests;

public class ErrorOrHttpExtensionsTests
{
    private static DefaultHttpContext CreateContext()
    {
        var http = new DefaultHttpContext();
        http.Items[HttpHeaderNames.CorrelationId] = "corr-42";
        return http;
    }

    [Fact]
    public void A_not_found_error_maps_to_a_404_problem()
    {
        var result = new List<Error> { Error.NotFound("quote.not_found", "Quote not found.") }
            .ToProblem(CreateContext());

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Extensions["errorCode"].ShouldBe("quote.not_found");
        problem.ProblemDetails.Extensions["correlationId"].ShouldBe("corr-42");
    }

    [Fact]
    public void A_conflict_error_maps_to_a_409_problem()
    {
        var result = new List<Error> { Error.Conflict("quote.duplicate_fingerprint", "Duplicate.") }
            .ToProblem(CreateContext());

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void A_validation_error_maps_to_a_400_validation_problem_keyed_by_error_code()
    {
        var errors = new List<Error>
        {
            Error.Validation("quote.text_too_short", "Quote text must be at least 12 characters."),
            Error.Validation("quote.text_needs_more_words", "Quote text must contain at least 3 words.")
        };

        var result = errors.ToProblem(CreateContext());

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        var validation = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        validation.Status.ShouldBe(StatusCodes.Status400BadRequest);
        validation.Errors.Keys.ShouldContain("quote.text_too_short");
        validation.Errors.Keys.ShouldContain("quote.text_needs_more_words");
    }

    [Fact]
    public void An_unexpected_error_maps_to_a_500_problem()
    {
        var result = new List<Error> { Error.Unexpected("error.boom", "Something broke.") }
            .ToProblem(CreateContext());

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void An_unauthorized_error_maps_to_a_401_problem()
    {
        var result = new List<Error> { Error.Unauthorized("auth.invalid_credentials", "Invalid credentials.") }
            .ToProblem(CreateContext());

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status401Unauthorized);
        problem.ProblemDetails.Extensions["errorCode"].ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public void A_forbidden_error_maps_to_a_403_problem()
    {
        var result = new List<Error> { Error.Forbidden("auth.forbidden", "Insufficient scope.") }
            .ToProblem(CreateContext());

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void A_single_error_overload_behaves_like_the_list()
    {
        var result = Error.NotFound("quote.not_found", "Quote not found.").ToProblem();

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Extensions.ShouldNotContainKey("correlationId");
    }
}
