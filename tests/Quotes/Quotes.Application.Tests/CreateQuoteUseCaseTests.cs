using ErrorOr;
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
        _quotes.AddAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>())
            .Returns(QuoteAddOutcome.Added);

        var result = await _sut.ExecuteAsync(
            new CreateQuoteCommand(
                "Refactoring is the art of improving design.",
                "Martin Fowler"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        var quote = result.Value!;
        quote.Text.ShouldBe("Refactoring is the art of improving design.");
        quote.Author.ShouldBe("Martin Fowler");
        await _quotes.Received(1).AddAsync(
            Arg.Is<Quote>(q => q != null
                && q.Text == "Refactoring is the art of improving design."
                && q.Author == "Martin Fowler"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_conflict_when_the_repository_reports_a_duplicate_fingerprint()
    {
        _quotes.AddAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>())
            .Returns(QuoteAddOutcome.DuplicateFingerprint);

        var result = await _sut.ExecuteAsync(
            new CreateQuoteCommand(
                "Talk is cheap. Show me the code!",
                "Someone Else"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.duplicate_fingerprint");
        result.FirstError.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task Returns_invalid_without_touching_the_repository()
    {
        var result = await _sut.ExecuteAsync(
            new CreateQuoteCommand("Nope.", "X"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_too_short");
        await _quotes.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }
}
