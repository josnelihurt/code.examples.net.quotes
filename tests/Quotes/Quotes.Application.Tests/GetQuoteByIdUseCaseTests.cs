using ErrorOr;
using NSubstitute;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application.Tests;

public class GetQuoteByIdUseCaseTests
{
    private static readonly Quote _sampleQuote = Quote.Reconstitute(
        "7",
        "Programs must be written for people to read.",
        "Harold Abelson",
        QuoteText.ComputeFingerprint("Programs must be written for people to read."));

    private readonly IQuoteRepository _quotes = Substitute.For<IQuoteRepository>();
    private readonly GetQuoteByIdUseCase _sut;

    public GetQuoteByIdUseCaseTests()
    {
        _sut = new GetQuoteByIdUseCase(_quotes);
    }

    [Fact]
    public async Task Returns_the_quote_for_a_known_id()
    {
        _quotes.GetByIdAsync(_sampleQuote.Id, Arg.Any<CancellationToken>()).Returns(_sampleQuote);

        var result = await _sut.ExecuteAsync(_sampleQuote.Id, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(_sampleQuote.Id);
        result.Value.Text.ShouldBe(_sampleQuote.Text.Value);
        result.Value.Author.ShouldBe(_sampleQuote.Author.Value);
    }

    [Fact]
    public async Task Returns_not_found_for_an_unknown_id()
    {
        _quotes.GetByIdAsync("missing", Arg.Any<CancellationToken>()).Returns((Quote?)null);

        var result = await _sut.ExecuteAsync("missing", TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.not_found");
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Honors_cancellation_before_loading()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync("7", cts.Token));

        await _quotes.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Returns_not_found_for_a_blank_id_without_touching_the_repository(string id)
    {
        var result = await _sut.ExecuteAsync(id, TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.not_found");
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        await _quotes.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, TestContext.Current.CancellationToken);
    }
}
