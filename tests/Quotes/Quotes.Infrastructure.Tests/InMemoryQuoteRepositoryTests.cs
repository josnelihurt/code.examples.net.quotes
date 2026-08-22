using NSubstitute;
using Quotes.Domain;
using Quotes.Infrastructure.Abstractions;

namespace Quotes.Infrastructure.Tests;

public class InMemoryQuoteRepositoryTests
{
    private const int SeedCount = 8;

    private readonly IQuoteSelector _selector = Substitute.For<IQuoteSelector>();
    private readonly InMemoryQuoteRepository _sut;

    public InMemoryQuoteRepositoryTests()
    {
        _sut = new InMemoryQuoteRepository(_selector);
    }

    [Fact]
    public void GetRandom_asks_the_selector_for_an_index_inside_the_catalogue()
    {
        _selector.NextIndex(Arg.Any<int>()).Returns(0);

        _sut.GetRandom();

        _selector.Received(1).NextIndex(SeedCount);
    }

    [Fact]
    public void Every_index_maps_to_a_fully_populated_quote()
    {
        for (var index = 0; index < SeedCount; index++)
        {
            _selector.NextIndex(Arg.Any<int>()).Returns(index);

            var quote = _sut.GetRandom();

            quote.Id.ShouldNotBeNullOrWhiteSpace();
            quote.Text.ShouldNotBeNullOrWhiteSpace();
            quote.Author.ShouldNotBeNullOrWhiteSpace();
            quote.NormalizedFingerprint.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Distinct_indexes_yield_distinct_quotes()
    {
        var ids = new List<string>();
        for (var index = 0; index < SeedCount; index++)
        {
            _selector.NextIndex(Arg.Any<int>()).Returns(index);
            ids.Add(_sut.GetRandom().Id);
        }

        ids.Distinct().Count().ShouldBe(SeedCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void An_out_of_range_index_is_rejected_rather_than_throwing_an_index_error(int index)
    {
        _selector.NextIndex(Arg.Any<int>()).Returns(index);

        Should.Throw<InvalidOperationException>(() => _sut.GetRandom());
    }

    [Fact]
    public void The_production_selector_stays_within_bounds()
    {
        var selector = new RandomQuoteSelector();

        for (var attempt = 0; attempt < 500; attempt++)
        {
            selector.NextIndex(8).ShouldBeInRange(0, 7);
        }
    }

    [Fact]
    public void ExistsByFingerprint_detects_seeded_quotes()
    {
        var fingerprint = Quote.ComputeFingerprint("Talk is cheap. Show me the code.");

        _sut.ExistsByFingerprint(fingerprint).ShouldBeTrue();
        _sut.ExistsByFingerprint("totally unique fingerprint").ShouldBeFalse();
    }

    [Fact]
    public void Add_persists_a_quote_available_to_GetRandom()
    {
        var created = Quote.Create(
            "Continuous delivery keeps software releasable.",
            "Jez Humble");
        created.Succeeded.ShouldBeTrue();
        var quote = created.Quote!;

        _sut.Add(quote);
        _sut.Count.ShouldBe(SeedCount + 1);
        _sut.ExistsByFingerprint(quote.NormalizedFingerprint).ShouldBeTrue();

        _selector.NextIndex(Arg.Any<int>()).Returns(SeedCount);
        var loaded = _sut.GetRandom();
        loaded.Id.ShouldBe(quote.Id);
        loaded.Text.ShouldBe(quote.Text);
        loaded.Author.ShouldBe(quote.Author);
    }
}
