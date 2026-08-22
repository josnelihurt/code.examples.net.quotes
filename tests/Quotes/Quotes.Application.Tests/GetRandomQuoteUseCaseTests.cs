using ErrorOr;
using NSubstitute;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application.Tests;

public class GetRandomQuoteUseCaseTests
{
    private static readonly Quote _sampleQuote = Quote.Reconstitute(
        "7",
        "Programs must be written for people to read.",
        "Harold Abelson",
        QuoteText.ComputeFingerprint("Programs must be written for people to read."));

    private readonly IQuoteRepository _quotes = Substitute.For<IQuoteRepository>();
    private readonly GetRandomQuoteUseCase _sut;

    public GetRandomQuoteUseCaseTests()
    {
        _sut = new GetRandomQuoteUseCase(_quotes);
    }

    [Fact]
    public async Task Returns_a_quote_from_the_repository()
    {
        _quotes.GetRandomAsync(Arg.Any<CancellationToken>()).Returns(_sampleQuote);

        var result = await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.Id.ShouldBe(_sampleQuote.Id);
        result.Value.Text.ShouldBe(_sampleQuote.Text.Value);
        result.Value.Author.ShouldBe(_sampleQuote.Author.Value);
    }

    [Fact]
    public async Task Returns_not_found_when_the_catalog_is_empty()
    {
        _quotes.GetRandomAsync(Arg.Any<CancellationToken>()).Returns((Quote?)null);

        var result = await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.not_found");
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Honors_cancellation_before_loading_a_quote()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync(cts.Token));

        await _quotes.DidNotReceiveWithAnyArgs().GetRandomAsync(TestContext.Current.CancellationToken);
    }
}
