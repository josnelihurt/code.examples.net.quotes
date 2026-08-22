namespace Quotes.Infrastructure.Abstractions;

/// <summary>
/// Picks which quote to serve. Extracted so tests can make the choice deterministic.
/// </summary>
public interface IQuoteSelector
{
    int NextIndex(int exclusiveUpperBound);
}
