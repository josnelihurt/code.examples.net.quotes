using NSubstitute;
using Quotes.Application.Abstractions;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application.Tests;

public class CreateQuoteUseCaseTests
{
    private readonly IQuoteRepository _quotes = Substitute.For<IQuoteRepository>();
    private readonly CreateQuoteUseCase _sut;

    public CreateQuoteUseCaseTests()
    {
        _sut = new CreateQuoteUseCase(_quotes);
    }

    [Fact]
    public async Task Creates_and_persists_a_valid_quote()
    {
        _quotes.ExistsByFingerprint(Arg.Any<string>()).Returns(false);

        var result = await _sut.ExecuteAsync(
            new CreateQuoteCommand(
                "Refactoring is the art of improving design.",
                "Martin Fowler"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CreateQuoteStatus.Created);
        result.Quote.ShouldNotBeNull();
        var quote = result.Quote!;
        quote.Text.ShouldBe("Refactoring is the art of improving design.");
        quote.Author.ShouldBe("Martin Fowler");
        _quotes.Received(1).Add(Arg.Is<Quote>(q =>
            q.Text == "Refactoring is the art of improving design."
            && q.Author == "Martin Fowler"));
    }

    [Fact]
    public async Task Returns_conflict_when_fingerprint_exists()
    {
        _quotes.ExistsByFingerprint(Arg.Any<string>()).Returns(true);

        var result = await _sut.ExecuteAsync(
            new CreateQuoteCommand(
                "Talk is cheap. Show me the code!",
                "Someone Else"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CreateQuoteStatus.Conflict);
        result.Quote.ShouldBeNull();
        _quotes.DidNotReceive().Add(Arg.Any<Quote>());
    }

    [Fact]
    public async Task Returns_invalid_without_touching_the_repository()
    {
        var result = await _sut.ExecuteAsync(
            new CreateQuoteCommand("Nope.", "X"),
            TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CreateQuoteStatus.Invalid);
        result.Error.ShouldBe(QuoteCreateError.TextTooShort);
        _quotes.DidNotReceive().ExistsByFingerprint(Arg.Any<string>());
        _quotes.DidNotReceive().Add(Arg.Any<Quote>());
    }
}
