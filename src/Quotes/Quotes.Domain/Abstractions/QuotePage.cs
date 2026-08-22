namespace Quotes.Domain.Abstractions;

/// <summary>
/// One page of the catalog in adapter order, plus the total item count so callers can
/// compute page counts without a second query.
/// </summary>
public sealed record QuotePage(IReadOnlyList<Quote> Items, int Total);
