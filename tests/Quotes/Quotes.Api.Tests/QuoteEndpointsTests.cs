using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quotes.Api.Contracts;
using Quotes.Api.Endpoints;
using Quotes.Application.Abstractions;
using Quotes.Domain;

namespace Quotes.Api.Tests;

public class QuoteEndpointsTests
{
    private static readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private static readonly QuoteDto _sampleQuote = new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly IGetRandomQuoteUseCase _useCase = Substitute.For<IGetRandomQuoteUseCase>();
    private readonly ICreateQuoteUseCase _createUseCase = Substitute.For<ICreateQuoteUseCase>();

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
    public async Task Create_returns_201_when_the_use_case_succeeds()
    {
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateQuoteResult(CreateQuoteStatus.Created, _sampleQuote));

        var http = CreateHttpContext();
        var result = await QuoteEndpoints.CreateAsync(
            new CreateQuoteRequestDto
            {
                Text = _sampleQuote.Text,
                Author = _sampleQuote.Author
            },
            _createUseCase,
            http,
            _loggerFactory,
            TestContext.Current.CancellationToken);

        var created = result.ShouldBeOfType<Created<QuoteResponseDto>>();
        created.Location.ShouldBe($"/api/quotes/{_sampleQuote.Id}");
        created.Value.ShouldNotBeNull();
        created.Value.Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task Create_returns_400_when_domain_validation_fails()
    {
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateQuoteResult(CreateQuoteStatus.Invalid, Error: QuoteCreateError.TextTooShort));

        var http = CreateHttpContext();
        var result = await QuoteEndpoints.CreateAsync(
            new CreateQuoteRequestDto { Text = "Nope.", Author = "Ada" },
            _createUseCase,
            http,
            _loggerFactory,
            TestContext.Current.CancellationToken);

        var json = result.ShouldBeOfType<JsonHttpResult<ErrorResponseDto>>();
        json.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        json.Value.ShouldNotBeNull();
        json.Value.Error.ShouldContain("at least");
    }

    [Fact]
    public async Task Create_returns_409_on_fingerprint_conflict()
    {
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateQuoteResult(CreateQuoteStatus.Conflict));

        var http = CreateHttpContext();
        var result = await QuoteEndpoints.CreateAsync(
            new CreateQuoteRequestDto
            {
                Text = "Talk is cheap. Show me the code!",
                Author = "Someone Else"
            },
            _createUseCase,
            http,
            _loggerFactory,
            TestContext.Current.CancellationToken);

        var json = result.ShouldBeOfType<JsonHttpResult<ErrorResponseDto>>();
        json.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Map_registers_quote_routes_with_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthentication().AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IGetRandomQuoteUseCase>());
        builder.Services.AddSingleton(Substitute.For<ICreateQuoteUseCase>());

        var app = builder.Build();
        try
        {
            QuoteEndpoints.Map(app);

            var endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .ToList();

            var random = endpoints.Single(e => e.RoutePattern.RawText == "/api/quotes/random");
            var create = endpoints.Single(e => e.RoutePattern.RawText == "/api/quotes/");

            random.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull();
            create.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull();
        }
        finally
        {
            ((IDisposable)app).Dispose();
        }
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        return http;
    }
}
