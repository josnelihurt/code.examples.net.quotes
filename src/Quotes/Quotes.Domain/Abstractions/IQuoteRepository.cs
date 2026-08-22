namespace Quotes.Domain.Abstractions;

/// <summary>
/// Outcome of an atomic add. The adapter owns duplicate detection so callers never race
/// between an existence check and an insert (in a database adapter this maps to catching
/// the unique-index violation).
/// </summary>
public enum QuoteAddOutcome
{
    Added,
    DuplicateFingerprint
}

public interface IQuoteRepository
{
    /// <summary>Returns a random quote, or <c>null</c> when the catalog is empty.</summary>
    Task<Quote?> GetRandomAsync(CancellationToken cancellationToken);

    /// <summary>Returns the quote with the given id, or <c>null</c> when it does not exist.</summary>
    Task<Quote?> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the <paramref name="take"/> items starting at offset <paramref name="skip"/>
    /// in stable catalog order, with the total item count. Offsets beyond the end return
    /// an empty page, never an error.
    /// </summary>
    Task<QuotePage> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<QuoteAddOutcome> AddAsync(Quote quote, CancellationToken cancellationToken);
}
