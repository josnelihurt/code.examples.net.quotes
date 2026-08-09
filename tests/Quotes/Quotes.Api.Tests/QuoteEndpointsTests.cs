using AspireQuotesPoc.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quotes.Api.Contracts;
using Quotes.Api.Endpoints;
using Quotes.Application;

namespace Quotes.Api.Tests;

public class QuoteEndpointsTests
{
    private static readonly NullLogger<QuoteEndpoints> Logger = NullLogger<QuoteEndpoints>.Instance;
    private static readonly QuoteDto SampleQuote = new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly IGetRandomQuoteUseCase _useCase = Substitute.For<IGetRandomQuoteUseCase>();

    [Fact]
    public async Task A_bearer_token_yields_the_quote_from_the_use_case()
    {
        _useCase.ExecuteAsync("token", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SampleQuote);

        using var host = TestHost.Create();
        host.Context.Request.Headers.Authorization = "Bearer token";

        var result = await QuoteEndpoints.GetRandomAsync(host.Context, _useCase, Logger, TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<QuoteResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Id.ShouldBe(SampleQuote.Id);
        ok.Value.Text.ShouldBe(SampleQuote.Text);
        ok.Value.Author.ShouldBe(SampleQuote.Author);
    }

    [Fact]
    public async Task The_request_correlation_id_is_forwarded_to_the_use_case()
    {
        _useCase.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SampleQuote);

        using var host = TestHost.Create();
        host.Context.Request.Headers.Authorization = "Bearer token";
        host.Context.Items[HttpHeaderNames.CorrelationId] = "corr-99";

        await QuoteEndpoints.GetRandomAsync(host.Context, _useCase, Logger, TestContext.Current.CancellationToken);

        await _useCase.Received(1).ExecuteAsync("token", "corr-99", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-without-scheme")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer")]
    [InlineData("Bearer    ")]
    public async Task A_missing_or_malformed_authorization_header_returns_401(string? header)
    {
        using var host = TestHost.Create();
        if (header is not null)
        {
            host.Context.Request.Headers.Authorization = header;
        }

        var result = await QuoteEndpoints.GetRandomAsync(host.Context, _useCase, Logger, TestContext.Current.CancellationToken);

        var json = result.ShouldBeOfType<JsonHttpResult<ErrorResponseDto>>();
        json.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        json.Value.ShouldNotBeNull();
        json.Value.Error.ShouldBe("Unauthorized");
        await _useCase.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_lowercase_bearer_scheme_is_accepted()
    {
        _useCase.ExecuteAsync("token", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SampleQuote);

        using var host = TestHost.Create();
        host.Context.Request.Headers.Authorization = "bearer token";

        var result = await QuoteEndpoints.GetRandomAsync(host.Context, _useCase, Logger, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<QuoteResponseDto>>();
    }

    [Fact]
    public async Task A_null_use_case_result_returns_401()
    {
        _useCase.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((QuoteDto?)null);

        using var host = TestHost.Create();
        host.Context.Request.Headers.Authorization = "Bearer stale";

        var result = await QuoteEndpoints.GetRandomAsync(host.Context, _useCase, Logger, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<JsonHttpResult<ErrorResponseDto>>()
            .StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Map_registers_the_random_quote_route()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(Substitute.For<IGetRandomQuoteUseCase>());

        var app = builder.Build();
        try
        {
            QuoteEndpoints.Map(app);

            var routes = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText);

            routes.ShouldBe(["/api/quotes/random"]);
        }
        finally
        {
            ((IDisposable)app).Dispose();
        }
    }
}
