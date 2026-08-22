using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quotes.Api.V1.Contracts;
using Quotes.Api.V1.Endpoints;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests;

public class QuoteEndpointsTests
{
    private static readonly ILogger<QuoteEndpointsLog> _logger = NullLogger<QuoteEndpointsLog>.Instance;
    private static readonly QuoteDto _sampleQuote = new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly ICreateQuoteUseCase _createUseCase = Substitute.For<ICreateQuoteUseCase>();
    private readonly IGetQuoteByIdUseCase _getByIdUseCase = Substitute.For<IGetQuoteByIdUseCase>();
    private readonly IGetRandomQuoteUseCase _useCase = Substitute.For<IGetRandomQuoteUseCase>();

    [Fact]
    public async Task Returns_the_quote_from_the_use_case()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        var response = await QuoteEndpoints.GetRandomAsync(
            _useCase, _logger, new DefaultHttpContext(), TestContext.Current.CancellationToken);

        var ok = response.ShouldBeOfType<Ok<QuoteResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Id.ShouldBe(_sampleQuote.Id);
        ok.Value.Text.ShouldBe(_sampleQuote.Text);
        ok.Value.Author.ShouldBe(_sampleQuote.Author);
    }

    [Fact]
    public async Task Returns_a_404_problem_when_the_catalog_is_empty()
    {
        ErrorOr<QuoteDto> result = Error.NotFound("quote.not_found", "Quote not found.");
        _useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        var response = await QuoteEndpoints.GetRandomAsync(
            _useCase, _logger, new DefaultHttpContext(), TestContext.Current.CancellationToken);

        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Forwards_the_cancellation_token_to_the_use_case()
    {
        using var cts = new CancellationTokenSource();
        ErrorOr<QuoteDto> result = _sampleQuote;
        _useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        await QuoteEndpoints.GetRandomAsync(_useCase, _logger, new DefaultHttpContext(), cts.Token);

        await _useCase.Received(1).ExecuteAsync(cts.Token);
    }

    [Fact]
    public async Task GetById_returns_the_quote_from_the_use_case()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _getByIdUseCase.ExecuteAsync(_sampleQuote.Id, Arg.Any<CancellationToken>()).Returns(result);

        var response = await QuoteEndpoints.GetByIdAsync(
            _sampleQuote.Id, _getByIdUseCase, _logger, new DefaultHttpContext(), TestContext.Current.CancellationToken);

        var ok = response.ShouldBeOfType<Ok<QuoteResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task GetById_returns_a_404_problem_for_an_unknown_id()
    {
        ErrorOr<QuoteDto> result = Error.NotFound("quote.not_found", "Quote not found.");
        _getByIdUseCase.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);

        var response = await QuoteEndpoints.GetByIdAsync(
            "missing", _getByIdUseCase, _logger, new DefaultHttpContext(), TestContext.Current.CancellationToken);

        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Create_returns_201_when_the_use_case_succeeds()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await QuoteEndpoints.CreateAsync(
            new CreateQuoteRequestDto
            {
                Text = _sampleQuote.Text,
                Author = _sampleQuote.Author
            },
            _createUseCase,
            new DefaultHttpContext(),
            _logger,
            TestContext.Current.CancellationToken);

        var created = response.ShouldBeOfType<CreatedAtRoute<QuoteResponseDto>>();
        created.RouteName.ShouldBe("GetQuoteById");
        created.RouteValues.ShouldNotBeNull();
        created.RouteValues["id"].ShouldBe(_sampleQuote.Id);
        created.Value.ShouldNotBeNull();
        created.Value.Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task Create_returns_a_400_problem_with_the_error_code_when_domain_validation_fails()
    {
        ErrorOr<QuoteDto> result = Error.Validation("quote.text_too_short", "Quote text must be at least 12 characters.");
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await QuoteEndpoints.CreateAsync(
            new CreateQuoteRequestDto { Text = "Nope.", Author = "Ada" },
            _createUseCase,
            new DefaultHttpContext(),
            _logger,
            TestContext.Current.CancellationToken);

        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        var validation = problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        validation.Errors.Keys.ShouldContain("quote.text_too_short");
    }

    [Fact]
    public async Task Create_returns_a_409_problem_on_a_fingerprint_conflict()
    {
        ErrorOr<QuoteDto> result = Error.Conflict("quote.duplicate_fingerprint", "A quote with the same meaning already exists.");
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await QuoteEndpoints.CreateAsync(
            new CreateQuoteRequestDto
            {
                Text = "Talk is cheap. Show me the code!",
                Author = "Someone Else"
            },
            _createUseCase,
            new DefaultHttpContext(),
            _logger,
            TestContext.Current.CancellationToken);

        var problem = response.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Map_registers_quote_routes_with_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthentication().AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IGetRandomQuoteUseCase>());
        builder.Services.AddSingleton(Substitute.For<IGetQuoteByIdUseCase>());
        builder.Services.AddSingleton(Substitute.For<ICreateQuoteUseCase>());

        var app = builder.Build();
        try
        {
            QuoteEndpoints.Map(app);

            var endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .ToList();

            var random = endpoints.Single(e => e.RoutePattern.RawText == "/api/v1/quotes/random");
            var byId = endpoints.Single(e => e.RoutePattern.RawText == "/api/v1/quotes/{id}");
            // The combined raw text renders with a trailing slash, but MapPost("") matches
            // the contract-documented form /api/v1/quotes (covered by the integration tests).
            var create = endpoints.Single(e => e.RoutePattern.RawText!.TrimEnd('/') == "/api/v1/quotes");

            random.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull()
                .Policy.ShouldBe(JwtAuthExtensions.ReadQuotesPolicy);
            byId.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull()
                .Policy.ShouldBe(JwtAuthExtensions.ReadQuotesPolicy);
            var createAuthorize = create.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull();
            createAuthorize.Policy.ShouldBe(JwtAuthExtensions.WriteQuotesPolicy);
        }
        finally
        {
            ((IDisposable)app).Dispose();
        }
    }
}
