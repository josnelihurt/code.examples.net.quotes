using Quotes.Domain;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;
using Quotes.Infrastructure.Mapping;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure;

public sealed class InMemoryQuoteRepository : IQuoteRepository
{
    private static readonly DateTimeOffset _seedCreatedAt =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly object _gate = new();
    private readonly List<QuoteRecord> _quotes;
    private readonly IQuoteSelector _selector;

    public InMemoryQuoteRepository(IQuoteSelector selector)
        : this(selector, DefaultSeed())
    {
    }

    /// <summary>Test seam: an empty or custom catalog, for the repository contract suite.</summary>
    internal InMemoryQuoteRepository(IQuoteSelector selector, List<QuoteRecord> initialQuotes)
    {
        _selector = selector;
        _quotes = [.. initialQuotes];
    }

    /// <summary>Catalog size. Test surface; production code goes through the port.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _quotes.Count;
            }
        }
    }

    internal static List<QuoteRecord> DefaultSeed() =>
    [
        QuoteMappingExtensions.Seed("1", "Simplicity is the ultimate sophistication.", "Leonardo da Vinci", _seedCreatedAt),
        QuoteMappingExtensions.Seed("2", "Code is like humor. When you have to explain it, it's bad.", "Cory House", _seedCreatedAt),
        QuoteMappingExtensions.Seed("3", "First, solve the problem. Then, write the code.", "John Johnson", _seedCreatedAt),
        QuoteMappingExtensions.Seed("4", "Experience is the name everyone gives to their mistakes.", "Oscar Wilde", _seedCreatedAt),
        QuoteMappingExtensions.Seed("5", "The only way to go fast is to go well.", "Robert C. Martin", _seedCreatedAt),
        QuoteMappingExtensions.Seed("6", "Make it work, make it right, make it fast.", "Kent Beck", _seedCreatedAt),
        QuoteMappingExtensions.Seed("7", "Programs must be written for people to read.", "Harold Abelson", _seedCreatedAt),
        QuoteMappingExtensions.Seed("8", "Talk is cheap. Show me the code.", "Linus Torvalds", _seedCreatedAt)
    ];

    public Task<Quote?> GetRandomAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Quote? quote = null;
        lock (_gate)
        {
            if (_quotes.Count > 0)
            {
                var index = _selector.NextIndex(_quotes.Count);
                if (index < 0 || index >= _quotes.Count)
                {
                    throw new InvalidOperationException(
                        $"Quote selector returned index {index}, outside 0..{_quotes.Count - 1}.");
                }

                quote = _quotes[index].ToDomain();
            }
        }

        return Task.FromResult(quote);
    }

    public Task<Quote?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        Quote? quote = null;
        lock (_gate)
        {
            var record = _quotes.FirstOrDefault(q => string.Equals(q.Id, id, StringComparison.Ordinal));
            if (record is not null)
            {
                quote = record.ToDomain();
            }
        }

        return Task.FromResult(quote);
    }

    public Task<QuoteAddOutcome> AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);
        cancellationToken.ThrowIfCancellationRequested();

        QuoteAddOutcome outcome;
        lock (_gate)
        {
            // Id collisions collapse into the same outcome: both mean "conflicts with an
            // existing entry" and the id is generated, so only a broken caller can hit it.
            outcome = _quotes.Exists(q =>
                    string.Equals(q.Id, quote.Id, StringComparison.Ordinal)
                    || string.Equals(q.NormalizedFingerprint, quote.Fingerprint.Value, StringComparison.Ordinal))
                ? QuoteAddOutcome.DuplicateFingerprint
                : QuoteAddOutcome.Added;

            if (outcome is QuoteAddOutcome.Added)
            {
                _quotes.Add(quote.ToRecord(DateTimeOffset.UtcNow));
            }
        }

        return Task.FromResult(outcome);
    }
}
