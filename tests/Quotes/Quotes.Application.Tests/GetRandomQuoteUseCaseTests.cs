using NSubstitute;
using Quotes.Domain;

namespace Quotes.Application.Tests;

public class GetRandomQuoteUseCaseTests
{
    private static readonly Quote SampleQuote = new()
    {
        Id = "7",
        Text = "Programs must be written for people to read.",
        Author = "Harold Abelson"
    };

    private readonly IAuthValidationClient _auth = Substitute.For<IAuthValidationClient>();
    private readonly IQuoteRepository _quotes = Substitute.For<IQuoteRepository>();
    private readonly GetRandomQuoteUseCase _sut;

    public GetRandomQuoteUseCaseTests()
    {
        _sut = new GetRandomQuoteUseCase(_auth, _quotes);
    }

    [Fact]
    public async Task Returns_a_quote_when_the_token_validates()
    {
        _auth.ValidateAsync("token", "corr", Arg.Any<CancellationToken>())
            .Returns(new AuthValidationResult(true, "jrb"));
        _quotes.GetRandom().Returns(SampleQuote);

        var result = await _sut.ExecuteAsync("token", "corr", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(SampleQuote.Id);
        result.Text.ShouldBe(SampleQuote.Text);
        result.Author.ShouldBe(SampleQuote.Author);
    }

    [Fact]
    public async Task Returns_null_and_skips_the_repository_when_validation_fails()
    {
        _auth.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthValidationResult(false, null));

        var result = await _sut.ExecuteAsync("stale", "corr", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        _quotes.DidNotReceive().GetRandom();
    }

    [Fact]
    public async Task Forwards_the_token_correlation_id_and_cancellation_token()
    {
        using var cts = new CancellationTokenSource();
        _auth.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthValidationResult(true, "jrb"));
        _quotes.GetRandom().Returns(SampleQuote);

        await _sut.ExecuteAsync("token", "corr", cts.Token);

        await _auth.Received(1).ValidateAsync("token", "corr", cts.Token);
    }

    [Fact]
    public async Task Propagates_failures_from_the_auth_client()
    {
        _auth.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<AuthValidationResult>(_ => throw new HttpRequestException("auth down"));

        await Should.ThrowAsync<HttpRequestException>(
            () => _sut.ExecuteAsync("token", "corr", TestContext.Current.CancellationToken));
    }
}
