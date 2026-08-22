using AspireQuotesPoc.ServiceDefaults.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceDefaults.Tests;

public class ValidationEndpointFilterTests
{
    private sealed record SampleRequest(string Name);

    private sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
    {
        public SampleRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    private static async Task<object?> InvokeAsync(
        ValidationEndpointFilter<SampleRequest> filter,
        SampleRequest? body)
    {
        var http = new DefaultHttpContext();
        http.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var context = new DefaultEndpointFilterInvocationContext(http, body);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok("next"));

        return await filter.InvokeAsync(context, next);
    }

    [Fact]
    public async Task A_null_body_produces_a_validation_problem()
    {
        var filter = new ValidationEndpointFilter<SampleRequest>(new SampleRequestValidator());

        var result = await InvokeAsync(filter, null);

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        var validation = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        validation.Errors.Keys.ShouldContain("$body");
    }

    [Fact]
    public async Task A_valid_body_reaches_the_next_delegate()
    {
        var filter = new ValidationEndpointFilter<SampleRequest>(new SampleRequestValidator());

        var result = await InvokeAsync(filter, new SampleRequest("jrb"));

        var ok = result.ShouldBeOfType<Ok<string>>();
        ok.Value.ShouldBe("next");
    }

    [Fact]
    public async Task An_invalid_body_reports_errors_grouped_by_property()
    {
        var filter = new ValidationEndpointFilter<SampleRequest>(new SampleRequestValidator());

        var result = await InvokeAsync(filter, new SampleRequest(""));

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        var validation = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        validation.Errors.Keys.ShouldContain(nameof(SampleRequest.Name));
    }

    [Fact]
    public async Task A_missing_validator_registration_fails_the_request_instead_of_skipping_validation()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        // Deliberately no IValidator<SampleRequest> registration.

        var app = builder.Build();
        app.MapPost("/sample", (SampleRequest body) => Results.Ok(body))
            .AddEndpointFilter<ValidationEndpointFilter<SampleRequest>>();
        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using var client = app.GetTestClient();

            // Fail-closed: the filter cannot be constructed without a validator, so the
            // request fails loudly instead of silently bypassing validation.
            var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
                client.PostAsync(
                    new Uri("/sample", UriKind.Relative),
                    new StringContent("""{"name":"anything"}""", System.Text.Encoding.UTF8, "application/json"),
                    TestContext.Current.CancellationToken));
            thrown.Message.ShouldContain("IValidator");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
