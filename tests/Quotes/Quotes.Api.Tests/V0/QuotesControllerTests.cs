using AspireQuotesPoc.ServiceDefaults.Errors;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Quotes.Api.V0.Contracts;
using Quotes.Api.V0.Controllers;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests.V0;

public class QuotesControllerTests
{
    private static readonly QuoteDto _sampleQuote =
        new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly ICreateQuoteUseCase _createUseCase = Substitute.For<ICreateQuoteUseCase>();
    private readonly IGetQuoteByIdUseCase _getByIdUseCase = Substitute.For<IGetQuoteByIdUseCase>();
    private readonly IGetRandomQuoteUseCase _randomUseCase = Substitute.For<IGetRandomQuoteUseCase>();
    private readonly IListQuotesUseCase _listUseCase = Substitute.For<IListQuotesUseCase>();

    private readonly QuotesController _sut;

    public QuotesControllerTests()
    {
        _sut = new QuotesController(_randomUseCase, _getByIdUseCase, _listUseCase, _createUseCase)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static ProblemDetails ProblemFrom<T>(ActionResult<T> response) =>
        response.Result.ShouldBeOfType<ProblemDetailsActionResult>().ProblemDetails;

    private static T ValueFrom<T>(ActionResult<T> response) =>
        response.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<T>();

    [Fact]
    public async Task GetRandom_returns_the_quote_from_the_use_case()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _randomUseCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.GetRandomAsync(TestContext.Current.CancellationToken);

        var quote = ValueFrom(response);
        quote.Id.ShouldBe(_sampleQuote.Id);
        quote.Text.ShouldBe(_sampleQuote.Text);
        quote.Author.ShouldBe(_sampleQuote.Author);
    }

    [Fact]
    public async Task GetRandom_returns_a_404_problem_when_the_catalog_is_empty()
    {
        ErrorOr<QuoteDto> result = Error.NotFound("quote.not_found", "Quote not found.");
        _randomUseCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.GetRandomAsync(TestContext.Current.CancellationToken);

        ProblemFrom(response).Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetRandom_forwards_the_cancellation_token_to_the_use_case()
    {
        using var cts = new CancellationTokenSource();
        ErrorOr<QuoteDto> result = _sampleQuote;
        _randomUseCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(result);

        await _sut.GetRandomAsync(cts.Token);

        await _randomUseCase.Received(1).ExecuteAsync(cts.Token);
    }

    [Fact]
    public async Task GetById_returns_the_quote_from_the_use_case()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _getByIdUseCase.ExecuteAsync("7", Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.GetByIdAsync("7", TestContext.Current.CancellationToken);

        ValueFrom(response).Id.ShouldBe("7");
    }

    [Fact]
    public async Task GetById_returns_a_404_problem_for_an_unknown_id()
    {
        ErrorOr<QuoteDto> result = Error.NotFound("quote.not_found", "Quote not found.");
        _getByIdUseCase.ExecuteAsync("nope", Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.GetByIdAsync("nope", TestContext.Current.CancellationToken);

        ProblemFrom(response).Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task List_passes_the_paging_query_through_to_the_use_case()
    {
        ErrorOr<QuotePageDto> result = new QuotePageDto([_sampleQuote], Page: 2, PageSize: 5, TotalItems: 8, TotalPages: 2);
        _listUseCase.ExecuteAsync(Arg.Any<ListQuotesQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.ListAsync(TestContext.Current.CancellationToken, page: 2, pageSize: 5);

        var page = ValueFrom(response);
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(5);
        page.TotalItems.ShouldBe(8);
        page.Items.Count.ShouldBe(1);
        await _listUseCase.Received(1).ExecuteAsync(
            Arg.Is<ListQuotesQuery>(q => q != null && q.Page == 2 && q.PageSize == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_returns_a_400_problem_for_an_out_of_range_page()
    {
        ErrorOr<QuotePageDto> result = Error.Validation("quote.invalid_page_request", "Out of range.");
        _listUseCase.ExecuteAsync(Arg.Any<ListQuotesQuery>(), Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.ListAsync(TestContext.Current.CancellationToken, page: 0);

        ProblemFrom(response).Status.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Create_maps_the_request_dto_onto_the_command()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(result);
        var body = new CreateQuoteRequestDto { Text = "A quote worth keeping.", Author = "Ada Lovelace" };

        await _sut.CreateAsync(body, TestContext.Current.CancellationToken);

        await _createUseCase.Received(1).ExecuteAsync(
            Arg.Is<CreateQuoteCommand>(c => c != null && c.Text == body.Text && c.Author == body.Author),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_returns_201_pointing_at_this_versions_route()
    {
        ErrorOr<QuoteDto> result = _sampleQuote;
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.CreateAsync(
            new CreateQuoteRequestDto { Text = "A quote worth keeping.", Author = "Ada Lovelace" },
            TestContext.Current.CancellationToken);

        var created = response.Result.ShouldBeOfType<CreatedAtRouteResult>();
        created.RouteName.ShouldBe(QuotesController.GetByIdRouteName);
        created.RouteValues!["id"].ShouldBe(_sampleQuote.Id);
        created.Value.ShouldBeOfType<QuoteResponseDto>().Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task Create_returns_a_409_problem_on_a_duplicate()
    {
        ErrorOr<QuoteDto> result = Error.Conflict("quote.duplicate_fingerprint", "Already exists.");
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.CreateAsync(
            new CreateQuoteRequestDto { Text = "A quote worth keeping.", Author = "Ada Lovelace" },
            TestContext.Current.CancellationToken);

        ProblemFrom(response).Status.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Create_returns_a_400_problem_when_the_domain_rejects_the_quote()
    {
        ErrorOr<QuoteDto> result = Error.Validation("quote.text_too_short", "Too short.");
        _createUseCase.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(result);

        var response = await _sut.CreateAsync(
            new CreateQuoteRequestDto { Text = "Short.", Author = "Ada Lovelace" },
            TestContext.Current.CancellationToken);

        var problem = ProblemFrom(response);
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Extensions["errorCode"].ShouldBe("quote.text_too_short");
    }
}
