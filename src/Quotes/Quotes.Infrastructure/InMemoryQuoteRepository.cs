using Quotes.Domain;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure;

public sealed class InMemoryQuoteRepository(IQuoteSelector selector) : IQuoteRepository
{
    private static readonly DateTimeOffset _seedCreatedAt =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<QuoteRecord> _quotes =
    [
        QuoteMapper.Seed("1", "Simplicity is the ultimate sophistication.", "Leonardo da Vinci", _seedCreatedAt),
        QuoteMapper.Seed("2", "Code is like humor. When you have to explain it, it's bad.", "Cory House", _seedCreatedAt),
        QuoteMapper.Seed("3", "First, solve the problem. Then, write the code.", "John Johnson", _seedCreatedAt),
        QuoteMapper.Seed("4", "Experience is the name everyone gives to their mistakes.", "Oscar Wilde", _seedCreatedAt),
        QuoteMapper.Seed("5", "The only way to go fast is to go well.", "Robert C. Martin", _seedCreatedAt),
        QuoteMapper.Seed("6", "Make it work, make it right, make it fast.", "Kent Beck", _seedCreatedAt),
        QuoteMapper.Seed("7", "Programs must be written for people to read.", "Harold Abelson", _seedCreatedAt),
        QuoteMapper.Seed("8", "Talk is cheap. Show me the code.", "Linus Torvalds", _seedCreatedAt)
    ];

    private readonly IQuoteSelector _selector = selector;
    private readonly object _gate = new();

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

    public Quote GetRandom()
    {
        lock (_gate)
        {
            var index = _selector.NextIndex(_quotes.Count);
            if (index < 0 || index >= _quotes.Count)
            {
                throw new InvalidOperationException(
                    $"Quote selector returned index {index}, outside 0..{_quotes.Count - 1}.");
            }

            return QuoteMapper.ToDomain(_quotes[index]);
        }
    }

    public bool ExistsByFingerprint(string normalizedFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedFingerprint);

        lock (_gate)
        {
            return _quotes.Exists(q =>
                string.Equals(q.NormalizedFingerprint, normalizedFingerprint, StringComparison.Ordinal));
        }
    }

    public void Add(Quote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        lock (_gate)
        {
            if (_quotes.Exists(q =>
                    string.Equals(q.Id, quote.Id, StringComparison.Ordinal)
                    || string.Equals(q.NormalizedFingerprint, quote.NormalizedFingerprint, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Quote '{quote.Id}' conflicts with an existing catalog entry.");
            }

            _quotes.Add(QuoteMapper.ToRecord(quote, DateTimeOffset.UtcNow));
        }
    }
}
