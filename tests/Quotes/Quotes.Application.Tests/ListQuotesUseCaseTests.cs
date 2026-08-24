using ErrorOr;
using NSubstitute;
using Quotes.Application.Abstractions;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application.Tests;

public class ListQuotesUseCaseTests
{
    private static readonly Quote _sampleQuote =
        Quote.Reconstitute(
            "7",
            "Programs must be written for people to read.",
            "Harold Abelson",
            "programs must be written for people to read");

    private readonly IQuoteRepository _quotes = Substitute.For<IQuoteRepository>();
    private readonly ListQuotesUseCase _sut;

    public ListQuotesUseCaseTests()
    {
        _sut = new ListQuotesUseCase(_quotes);
    }

    [Fact]
    public async Task ExecuteAsync_returns_a_page_with_the_paging_arithmetic()
    {
        _quotes.ListAsync(3, 3, Arg.Any<CancellationToken>())
            .Returns(new QuotePage([_sampleQuote, _sampleQuote, _sampleQuote], 8));

        var result = await _sut.ExecuteAsync(
            new ListQuotesQuery(2, 3),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(3);
        result.Value.Items.Count.ShouldBe(3);
        result.Value.TotalItems.ShouldBe(8);
        result.Value.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_rounds_total_pages_up()
    {
        _quotes.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new QuotePage([], 7));

        var result = await _sut.ExecuteAsync(
            new ListQuotesQuery(3, 3),
            TestContext.Current.CancellationToken);

        result.Value.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteAsync_translates_the_1_based_page_into_a_skip_offset()
    {
        _quotes.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new QuotePage([], 0));

        await _sut.ExecuteAsync(new ListQuotesQuery(4, 25), TestContext.Current.CancellationToken);

        await _quotes.Received(1).ListAsync(75, 25, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(QuoteRules.MaxPage + 1, 20)]
    [InlineData(int.MaxValue, 100)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, 101)]
    public async Task ExecuteAsync_rejects_pages_outside_the_allowed_range_without_touching_the_repository(
        int page, int pageSize)
    {
        var result = await _sut.ExecuteAsync(
            new ListQuotesQuery(page, pageSize),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.invalid_page_request");
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        await _quotes.DidNotReceiveWithAnyArgs().ListAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_accepts_the_maximum_page()
    {
        _quotes.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new QuotePage([], 0));

        var result = await _sut.ExecuteAsync(
            new ListQuotesQuery(QuoteRules.MaxPage, QuoteRules.MaxPageSize),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        await _quotes.Received(1)
            .ListAsync((QuoteRules.MaxPage - 1) * QuoteRules.MaxPageSize, QuoteRules.MaxPageSize, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_accepts_the_maximum_page_size()
    {
        _quotes.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new QuotePage([_sampleQuote], 1));

        var result = await _sut.ExecuteAsync(
            new ListQuotesQuery(1, QuoteRules.MaxPageSize),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        await _quotes.Received(1).ListAsync(0, QuoteRules.MaxPageSize, Arg.Any<CancellationToken>());
    }
}
