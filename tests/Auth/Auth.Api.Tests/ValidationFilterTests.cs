using Auth.Api.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Api.Tests;

public class ValidationFilterTests
{
    private static HttpValidationProblemDetails ProblemDetailsOf(IResult? result)
    {
        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        return problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
    }

    [Fact]
    public async Task A_null_body_produces_a_validation_problem()
    {
        using var host = TestHost.Create();

        var result = await ValidationFilter.ValidateAsync<LoginRequestDto>(null, host.Context);

        ProblemDetailsOf(result).Errors.ShouldContainKey("");
    }

    [Fact]
    public async Task A_valid_body_passes_through()
    {
        using var host = TestHost.Create(services =>
            services.AddSingleton<IValidator<LoginRequestDto>, LoginRequestDtoValidator>());

        var result = await ValidationFilter.ValidateAsync(
            new LoginRequestDto { Username = "jrb", Password = "supersecret" },
            host.Context);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task An_invalid_body_reports_errors_grouped_by_property()
    {
        using var host = TestHost.Create(services =>
            services.AddSingleton<IValidator<LoginRequestDto>, LoginRequestDtoValidator>());

        var result = await ValidationFilter.ValidateAsync(
            new LoginRequestDto { Username = "", Password = "" },
            host.Context);

        var errors = ProblemDetailsOf(result).Errors;
        errors.ShouldContainKey(nameof(LoginRequestDto.Username));
        errors.ShouldContainKey(nameof(LoginRequestDto.Password));
    }

    [Fact]
    public async Task A_missing_validator_lets_the_request_through()
    {
        using var host = TestHost.Create();

        var result = await ValidationFilter.ValidateAsync(
            new LoginRequestDto { Username = "anything", Password = "goes" },
            host.Context);

        result.ShouldBeNull();
    }
}
