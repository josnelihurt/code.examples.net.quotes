namespace Quotes.Infrastructure;

/// <summary>
/// Picks which quote to serve. Extracted so tests can make the choice deterministic.
/// </summary>
public interface IQuoteSelector
{
    int NextIndex(int exclusiveUpperBound);
}

public sealed class RandomQuoteSelector : IQuoteSelector
{
    public int NextIndex(int exclusiveUpperBound) => Random.Shared.Next(exclusiveUpperBound);
}
