using System.Text;
using System.Text.Json.Nodes;
using AspireQuotesPoc.ServiceDefaults.Errors;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Quotes.Api.V2.Endpoints;
using Quotes.Application.Abstractions;
using Quotes.Domain;

namespace Quotes.Api.Tests.V2;

/// <summary>
/// Handler-level tests for the v2 HTTP adapter, mirroring the v1 <c>QuoteEndpointsTests</c>:
/// the handlers take an <see cref="HttpContext"/> (not bound use cases), so each test
/// executes the returned <see cref="IResult"/> against a <see cref="DefaultHttpContext"/>
/// wired to a <see cref="Quotes.Api.V2.Services.QuoteGrpcService"/> over NSubstitute use
/// cases and asserts on the finished response — status, media type, Location and parsed
/// JSON body. That executes the full in-process path (JSON-PB binding, contract validation,
/// the gRPC error bridge and <see cref="Quotes.Api.V2.Proto.ProtoJsonResult"/> writing)
/// without a host; route/policy mapping lives in <see cref="ProtoConformanceTests"/> and
/// wire parity in <c>VersionParityTests</c>.
/// </summary>
public class QuoteEndpointsTests
{
    private const string _createdLocation = "http://localhost/api/v2/quotes/7";

    private static readonly QuoteDto _sampleQuote =
        new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly ICreateQuoteUseCase _createUseCase = Substitute.For<ICreateQuoteUseCase>();
    private readonly IGetQuoteByIdUseCase _getByIdUseCase = Substitute.For<IGetQuoteByIdUseCase>();
    private readonly IGetRandomQuoteUseCase _randomUseCase = Substitute.For<IGetRandomQuoteUseCase>();
    private readonly IListQuotesUseCase _listUseCase = Substitute.For<IListQuotesUseCase>();

    private readonly LinkGenerator _linkGenerator = Substitute.For<LinkGenerator>();

    public QuoteEndpointsTests()
    {
        // The adapter generates the Location header through LinkGenerator.GetUriByName,
        // which forwards to the HttpContext-based GetUriByAddress overload with the
        // endpoint name itself as the address.
        _linkGenerator.GetUriByAddress(
                Arg.Any<HttpContext>(),
                Arg.Any<string>(),
                Arg.Any<RouteValueDictionary>(),
                Arg.Any<RouteValueDictionary?>(),
                Arg.Any<string?>(),
                Arg.Any<HostString?>(),
                Arg.Any<PathString?>(),
                Arg.Any<FragmentString>(),
                Arg.Any<LinkOptions?>())
            .Returns(_createdLocation);
    }

    private static async Task<JsonNode> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return JsonNode.Parse(await reader.ReadToEndAsync(TestContext.Current.CancellationToken))!;
    }

    private DefaultHttpContext NewContext()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_randomUseCase)
            .AddSingleton(_getByIdUseCase)
            .AddSingleton(_listUseCase)
            .AddSingleton(_createUseCase)
            .AddScoped<Quotes.Api.V2.Services.QuoteGrpcService>()
            .BuildServiceProvider();

        // DefaultHttpContext discards writes into a NullStream in .NET 10, so the response
        // needs a real buffer for the body assertions to read back.
        return new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
    }

    private async Task<DefaultHttpContext> ExecuteAsync(
        Func<HttpContext, CancellationToken, Task<IResult>> handler,
        string? requestBody = null)
    {
        var context = NewContext();
        if (requestBody is not null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody));
        }

        var result = await handler(context, TestContext.Current.CancellationToken);
        await result.ExecuteAsync(context);
        return context;
    }

    private Task<DefaultHttpContext> CreateAsync(string requestBody) =>
        ExecuteAsync(
            (http, token) => Quotes.Api.V2.Endpoints.QuoteEndpoints.CreateAsync(
                http.Request, http, _linkGenerator, token),
            requestBody);

    [Fact]
    public async Task GetRandom_answers_200_with_the_quote_as_protobuf_formatted_json()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _randomUseCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        var context = await ExecuteAsync(QuoteEndpoints.GetRandomAsync);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        context.Response.ContentType.ShouldBe("application/json; charset=utf-8");
        var body = await ReadBodyAsync(context);
        body["id"]!.GetValue<string>().ShouldBe(_sampleQuote.Id);
        body["text"]!.GetValue<string>().ShouldBe(_sampleQuote.Text);
        body["author"]!.GetValue<string>().ShouldBe(_sampleQuote.Author);
    }

    [Fact]
    public async Task GetRandom_forwards_the_cancellation_token()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _randomUseCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);
        using var cts = new CancellationTokenSource();
        var context = NewContext();

        await (await QuoteEndpoints.GetRandomAsync(context, cts.Token)).ExecuteAsync(context);

        await _randomUseCase.Received(1).ExecuteAsync(cts.Token);
    }

    [Fact]
    public async Task GetRandom_answers_the_shared_404_problem_when_the_catalog_is_empty()
    {
        ErrorOr<QuoteDto> notFound = Error.NotFound("quote.not_found", "Quote not found.");
        _randomUseCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(notFound);

        var context = await ExecuteAsync(QuoteEndpoints.GetRandomAsync);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var problem = await ReadBodyAsync(context);
        problem["title"]!.GetValue<string>().ShouldBe("Not Found");
        problem["errorCode"]!.GetValue<string>().ShouldBe("quote.not_found");
        problem["correlationId"].ShouldNotBeNull();
    }

    [Fact]
    public async Task List_answers_200_emitting_default_valued_paging_scalars()
    {
        ErrorOr<QuotePageDto> page = new QuotePageDto([_sampleQuote], 1, 20, 8, 1);
        _listUseCase.ExecuteAsync(Arg.Any<ListQuotesQuery>(), Arg.Any<CancellationToken>()).Returns(page);

        var context = await ExecuteAsync(
            (http, token) => QuoteEndpoints.ListAsync(http, token, page: 1, pageSize: 20));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var body = await ReadBodyAsync(context);
        body["page"]!.GetValue<int>().ShouldBe(1);
        body["pageSize"]!.GetValue<int>().ShouldBe(20);
        body["totalItems"]!.GetValue<int>().ShouldBe(8);
        body["totalPages"]!.GetValue<int>().ShouldBe(1);
        body["items"]!.AsArray().Single()!["id"]!.GetValue<string>().ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task List_answers_a_400_problem_for_an_invalid_page_request()
    {
        ErrorOr<QuotePageDto> rejected = QuoteErrors.InvalidPageRequest;
        _listUseCase.ExecuteAsync(Arg.Any<ListQuotesQuery>(), Arg.Any<CancellationToken>())
            .Returns(rejected);

        var context = await ExecuteAsync((http, token) => QuoteEndpoints.ListAsync(http, token));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var problem = await ReadBodyAsync(context);
        problem["errorCode"]!.GetValue<string>().ShouldBe("quote.invalid_page_request");
    }

    [Fact]
    public async Task GetById_answers_200_with_the_quote()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _getByIdUseCase.ExecuteAsync(_sampleQuote.Id, Arg.Any<CancellationToken>()).Returns(result);

        var context = NewContext();
        await (await QuoteEndpoints.GetByIdAsync(_sampleQuote.Id, context, TestContext.Current.CancellationToken))
            .ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        (await ReadBodyAsync(context))["id"]!.GetValue<string>().ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task GetById_answers_the_shared_404_problem_for_an_unknown_id()
    {
        ErrorOr<QuoteDto> notFound = Error.NotFound("quote.not_found", "Quote not found.");
        _getByIdUseCase.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(notFound);

        var context = NewContext();
        await (await QuoteEndpoints.GetByIdAsync("missing", context, TestContext.Current.CancellationToken))
            .ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var problem = await ReadBodyAsync(context);
        problem["detail"]!.GetValue<string>().ShouldBe("Quote not found.");
        problem["errorCode"]!.GetValue<string>().ShouldBe("quote.not_found");
    }

    [Fact]
    public async Task Create_answers_201_with_the_location_inside_the_v2_namespace()
    {
        ErrorOr<QuoteDto> created = _sampleQuote;
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(created);

        var context = await CreateAsync(
            """{"text":"Programs must be written for people to read.","author":"Harold Abelson"}""");

        context.Response.StatusCode.ShouldBe(StatusCodes.Status201Created);
        context.Response.Headers.Location.ToString().ShouldBe(_createdLocation);
        context.Response.ContentType.ShouldBe("application/json; charset=utf-8");
        (await ReadBodyAsync(context))["id"]!.GetValue<string>().ShouldBe(_sampleQuote.Id);

        _linkGenerator.Received(1).GetUriByAddress(
            Arg.Any<HttpContext>(),
            Arg.Is<string>(address => address == QuoteEndpoints.GetByIdRouteName),
            Arg.Any<RouteValueDictionary>(),
            Arg.Any<RouteValueDictionary?>(),
            Arg.Any<string?>(),
            Arg.Any<HostString?>(),
            Arg.Any<PathString?>(),
            Arg.Any<FragmentString>(),
            Arg.Any<LinkOptions?>());
    }

    [Fact]
    public async Task Create_answers_a_400_problem_keyed_by_error_code_when_domain_validation_fails()
    {
        ErrorOr<QuoteDto> tooShort = Error.Validation(
            "quote.text_too_short", "Quote text must be at least 12 characters.");
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(tooShort);

        var context = await CreateAsync("""{"text":"Short.","author":"Ada Lovelace"}""");

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var problem = await ReadBodyAsync(context);
        problem["errors"]!["quote.text_too_short"]!.AsArray().Single()!.GetValue<string>()
            .ShouldBe("Quote text must be at least 12 characters.");
        problem["errorCode"]!.GetValue<string>().ShouldBe("quote.text_too_short");
    }

    [Fact]
    public async Task Create_answers_a_409_problem_on_a_fingerprint_conflict()
    {
        ErrorOr<QuoteDto> conflict = Error.Conflict(
            "quote.duplicate_fingerprint", "A quote with the same meaning already exists.");
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(conflict);

        var context = await CreateAsync("""{"text":"Talk is cheap. Show me the code!","author":"Someone Else"}""");

        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var problem = await ReadBodyAsync(context);
        problem["errorCode"]!.GetValue<string>().ShouldBe("quote.duplicate_fingerprint");
    }

    [Fact]
    public async Task Create_answers_a_400_problem_keyed_by_field_when_contract_validation_fails()
    {
        var context = await CreateAsync("""{"text":"","author":""}""");

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var problem = await ReadBodyAsync(context);
        problem["errors"]!["Text"]!.AsArray().Single()!.GetValue<string>()
            .ShouldBe("The Text field is required.");
        problem["errors"]!["Author"]!.AsArray().Single()!.GetValue<string>()
            .ShouldBe("The Author field is required.");
        problem["errorCode"]!.GetValue<string>().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    public async Task Create_answers_a_400_problem_when_the_body_is_not_json(string requestBody)
    {
        var context = await CreateAsync(requestBody);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var problem = await ReadBodyAsync(context);
        problem["detail"]!.GetValue<string>().ShouldBe("The request body could not be read as JSON.");
        problem["errorCode"]!.GetValue<string>().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
    }

    [Fact]
    public async Task Create_forwards_the_bound_fields_and_the_cancellation_token()
    {
        ErrorOr<QuoteDto> created = _sampleQuote;
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(created);
        using var cts = new CancellationTokenSource();
        var context = NewContext();
        context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes("""{"text":"Programs must be written for people to read.","author":"Harold Abelson"}"""));

        var result = await Quotes.Api.V2.Endpoints.QuoteEndpoints.CreateAsync(
            context.Request, context, _linkGenerator, cts.Token);
        await result.ExecuteAsync(context);

        await _createUseCase.Received(1).ExecuteAsync(
            new CreateQuoteCommand(_sampleQuote.Text, _sampleQuote.Author),
            cts.Token);
    }
}
