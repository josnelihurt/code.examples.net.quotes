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
        loaded.Text.ShouldBe(created.Value.Text);
        loaded.Author.ShouldBe(created.Value.Author);
        loaded.NormalizedFingerprint.ShouldBe(created.Value.NormalizedFingerprint);
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
        stored.Text.ShouldBe(first.Value.Text);
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
}
