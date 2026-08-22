using NSubstitute;
using Quotes.Domain;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;

namespace Quotes.Infrastructure.Tests;

public class InMemoryQuoteRepositoryTests : QuoteRepositoryContractTests
{
    private const int _seedCount = 8;

    private readonly IQuoteSelector _selector = Substitute.For<IQuoteSelector>();
    private readonly InMemoryQuoteRepository _sut;

    public InMemoryQuoteRepositoryTests()
    {
        _sut = new InMemoryQuoteRepository(_selector);
    }

    protected override Task<IQuoteRepository> CreateRepositoryAsync() =>
        Task.FromResult<IQuoteRepository>(new InMemoryQuoteRepository(_selector, []));

    [Fact]
    public async Task GetRandomAsync_asks_the_selector_for_an_index_inside_the_catalogue()
    {
        _selector.NextIndex(Arg.Any<int>()).Returns(0);

        await _sut.GetRandomAsync(TestContext.Current.CancellationToken);

        _selector.Received(1).NextIndex(_seedCount);
    }

    [Fact]
    public async Task Every_index_maps_to_a_fully_populated_quote()
    {
        for (var index = 0; index < _seedCount; index++)
        {
            _selector.NextIndex(Arg.Any<int>()).Returns(index);

            var quote = await _sut.GetRandomAsync(TestContext.Current.CancellationToken);

            quote.ShouldNotBeNull();
            quote.Id.ShouldNotBeNullOrWhiteSpace();
            quote.Text.ShouldNotBeNullOrWhiteSpace();
            quote.Author.ShouldNotBeNullOrWhiteSpace();
            quote.NormalizedFingerprint.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task Distinct_indexes_yield_distinct_quotes()
    {
        var ids = new List<string>();
        for (var index = 0; index < _seedCount; index++)
        {
            _selector.NextIndex(Arg.Any<int>()).Returns(index);
            var quote = await _sut.GetRandomAsync(TestContext.Current.CancellationToken);
            ids.Add(quote!.Id);
        }

        ids.Distinct().Count().ShouldBe(_seedCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task An_out_of_range_index_is_rejected_rather_than_throwing_an_index_error(int index)
    {
        _selector.NextIndex(Arg.Any<int>()).Returns(index);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.GetRandomAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_production_selector_stays_within_bounds()
    {
        var selector = new RandomQuoteSelector();

        for (var attempt = 0; attempt < 500; attempt++)
        {
            selector.NextIndex(_seedCount).ShouldBeInRange(0, _seedCount - 1);
        }
    }

    [Fact]
    public async Task GetByIdAsync_resolves_a_seeded_quote()
    {
        var quote = await _sut.GetByIdAsync("7", TestContext.Current.CancellationToken);

        quote.ShouldNotBeNull();
        quote.Author.ShouldBe("Harold Abelson");
    }

    [Fact]
    public async Task AddAsync_persists_a_quote_available_to_GetRandomAsync()
    {
        var created = Quote.Create("Continuous delivery keeps software releasable.", "Jez Humble");
        await _sut.AddAsync(created.Value, TestContext.Current.CancellationToken);

        _sut.Count.ShouldBe(_seedCount + 1);

        _selector.NextIndex(Arg.Any<int>()).Returns(_seedCount);
        var loaded = await _sut.GetRandomAsync(TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(created.Value.Id);
        loaded.Text.ShouldBe(created.Value.Text);
        loaded.Author.ShouldBe(created.Value.Author);
    }
}
