using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Infrastructure.Tests;

/// <summary>
/// Contract every <see cref="IQuoteRepository"/> adapter must satisfy. Point a future
/// adapter's test project at this class to prove the InMemory swap is behavior-preserving.
/// </summary>
public abstract class QuoteRepositoryContractTests
{
    protected abstract Task<IQuoteRepository> CreateRepositoryAsync();

    [Fact]
    public async Task GetRandomAsync_returns_null_on_an_empty_catalog()
    {
        var repository = await CreateRepositoryAsync();

        var quote = await repository.GetRandomAsync(TestContext.Current.CancellationToken);

        quote.ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_round_trips_through_GetByIdAsync()
    {
        var repository = await CreateRepositoryAsync();
        var created = Quote.Create("Continuous delivery keeps software releasable.", "Jez Humble");

        var outcome = await repository.AddAsync(created.Value, TestContext.Current.CancellationToken);

        outcome.ShouldBe(QuoteAddOutcome.Added);
        var loaded = await repository.GetByIdAsync(created.Value.Id, TestContext.Current.CancellationToken);
        loaded.ShouldNotBeNull();
        loaded.Text.Value.ShouldBe(created.Value.Text.Value);
        loaded.Author.Value.ShouldBe(created.Value.Author.Value);
        loaded.Fingerprint.Value.ShouldBe(created.Value.Fingerprint.Value);
    }

    [Fact]
    public async Task AddAsync_reports_a_duplicate_fingerprint_atomically()
    {
        var repository = await CreateRepositoryAsync();
        var first = Quote.Create("Talk is cheap. Show me the code.", "Linus Torvalds");
        // Different punctuation and case, so a different instance and id, same fingerprint.
        var nearDuplicate = Quote.Create("Talk is cheap, show me the code!", "Someone Else");

        await repository.AddAsync(first.Value, TestContext.Current.CancellationToken);
        var outcome = await repository.AddAsync(nearDuplicate.Value, TestContext.Current.CancellationToken);

        outcome.ShouldBe(QuoteAddOutcome.DuplicateFingerprint);
        var stored = await repository.GetByIdAsync(first.Value.Id, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Text.Value.ShouldBe(first.Value.Text.Value);
    }

    [Fact]
    public async Task GetRandomAsync_returns_the_only_quote_in_the_catalog()
    {
        var repository = await CreateRepositoryAsync();
        var created = Quote.Create("Continuous delivery keeps software releasable.", "Jez Humble");
        await repository.AddAsync(created.Value, TestContext.Current.CancellationToken);

        var quote = await repository.GetRandomAsync(TestContext.Current.CancellationToken);

        quote.ShouldNotBeNull();
        quote.Id.ShouldBe(created.Value.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_id()
    {
        var repository = await CreateRepositoryAsync();

        var quote = await repository.GetByIdAsync("does-not-exist", TestContext.Current.CancellationToken);

        quote.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_pages_the_catalog_without_overlap_and_reports_the_total()
    {
        var repository = await CreateRepositoryAsync();
        var created = new[]
        {
            Quote.Create("Continuous delivery keeps software releasable.", "Jez Humble").Value,
            Quote.Create("Talk is cheap. Show me the code.", "Linus Torvalds").Value,
            Quote.Create("Make it work, make it right, make it fast.", "Kent Beck").Value
        };
        foreach (var quote in created)
        {
            await repository.AddAsync(quote, TestContext.Current.CancellationToken);
        }

        var firstPage = await repository.ListAsync(0, 2, TestContext.Current.CancellationToken);
        var secondPage = await repository.ListAsync(2, 2, TestContext.Current.CancellationToken);

        firstPage.Items.Count.ShouldBe(2);
        firstPage.Total.ShouldBe(3);
        secondPage.Items.Count.ShouldBe(1);
        secondPage.Total.ShouldBe(3);
        firstPage.Items.Select(quote => quote.Id)
            .Intersect(secondPage.Items.Select(quote => quote.Id))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_returns_an_empty_page_beyond_the_end_instead_of_failing()
    {
        var repository = await CreateRepositoryAsync();
        var created = Quote.Create("Continuous delivery keeps software releasable.", "Jez Humble").Value;
        await repository.AddAsync(created, TestContext.Current.CancellationToken);

        var page = await repository.ListAsync(10, 5, TestContext.Current.CancellationToken);

        page.Items.ShouldBeEmpty();
        page.Total.ShouldBe(1);
    }

    [Fact]
    public async Task ListAsync_is_stable_across_repeated_reads_of_the_same_page()
    {
        var repository = await CreateRepositoryAsync();
        var created = new[]
        {
            Quote.Create("Continuous delivery keeps software releasable.", "Jez Humble").Value,
            Quote.Create("Talk is cheap. Show me the code.", "Linus Torvalds").Value
        };
        foreach (var quote in created)
        {
            await repository.AddAsync(quote, TestContext.Current.CancellationToken);
        }

        var first = await repository.ListAsync(0, 2, TestContext.Current.CancellationToken);
        var second = await repository.ListAsync(0, 2, TestContext.Current.CancellationToken);

        first.Items.Select(quote => quote.Id)
            .ShouldBe(second.Items.Select(quote => quote.Id));
    }
}
