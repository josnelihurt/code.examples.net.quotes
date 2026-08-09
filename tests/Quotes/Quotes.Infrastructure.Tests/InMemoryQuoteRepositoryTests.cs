using NSubstitute;

namespace Quotes.Infrastructure.Tests;

public class InMemoryQuoteRepositoryTests
{
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

        _selector.Received(1).NextIndex(InMemoryQuoteRepository.Count);
    }

    [Fact]
    public void Every_index_maps_to_a_fully_populated_quote()
    {
        for (var index = 0; index < InMemoryQuoteRepository.Count; index++)
        {
            _selector.NextIndex(Arg.Any<int>()).Returns(index);

            var quote = _sut.GetRandom();

            quote.Id.ShouldNotBeNullOrWhiteSpace();
            quote.Text.ShouldNotBeNullOrWhiteSpace();
            quote.Author.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Distinct_indexes_yield_distinct_quotes()
    {
        var ids = new List<string>();
        for (var index = 0; index < InMemoryQuoteRepository.Count; index++)
        {
            _selector.NextIndex(Arg.Any<int>()).Returns(index);
            ids.Add(_sut.GetRandom().Id);
        }

        ids.Distinct().Count().ShouldBe(InMemoryQuoteRepository.Count);
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
}
