using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quotes.Api.Contracts;
using Quotes.Api.Endpoints;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests;

public class QuoteEndpointsTests
{
    private static readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private static readonly QuoteDto _sampleQuote = new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly IGetRandomQuoteUseCase _useCase = Substitute.For<IGetRandomQuoteUseCase>();

    [Fact]
    public async Task Returns_the_quote_from_the_use_case()
    {
        _useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(_sampleQuote);

        var result = await QuoteEndpoints.GetRandomAsync(_useCase, _loggerFactory, TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<QuoteResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Id.ShouldBe(_sampleQuote.Id);
        ok.Value.Text.ShouldBe(_sampleQuote.Text);
        ok.Value.Author.ShouldBe(_sampleQuote.Author);
    }

    [Fact]
    public async Task Forwards_the_cancellation_token_to_the_use_case()
    {
        using var cts = new CancellationTokenSource();
        _useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(_sampleQuote);

        await QuoteEndpoints.GetRandomAsync(_useCase, _loggerFactory, cts.Token);

        await _useCase.Received(1).ExecuteAsync(cts.Token);
    }

    [Fact]
    public void Map_registers_the_random_quote_route_with_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthentication().AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IGetRandomQuoteUseCase>());

        var app = builder.Build();
        try
        {
            QuoteEndpoints.Map(app);

            var endpoint = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Single(e => e.RoutePattern.RawText == "/api/quotes/random");

            endpoint.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull();
        }
        finally
        {
            ((IDisposable)app).Dispose();
        }
    }
}
