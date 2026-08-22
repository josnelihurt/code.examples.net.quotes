using NSubstitute;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application.Tests;

public class GetRandomQuoteUseCaseTests
{
    private static readonly Quote _sampleQuote = new()
    {
        Id = "7",
        Text = "Programs must be written for people to read.",
        Author = "Harold Abelson"
    };

    private readonly IQuoteRepository _quotes = Substitute.For<IQuoteRepository>();
    private readonly GetRandomQuoteUseCase _sut;

    public GetRandomQuoteUseCaseTests()
    {
        _sut = new GetRandomQuoteUseCase(_quotes);
    }

    [Fact]
    public async Task Returns_a_quote_from_the_repository()
    {
        _quotes.GetRandom().Returns(_sampleQuote);

        var result = await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        result.Id.ShouldBe(_sampleQuote.Id);
        result.Text.ShouldBe(_sampleQuote.Text);
        result.Author.ShouldBe(_sampleQuote.Author);
    }

    [Fact]
    public async Task Honors_cancellation_before_loading_a_quote()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.ExecuteAsync(cts.Token));

        _quotes.DidNotReceive().GetRandom();
    }
}
