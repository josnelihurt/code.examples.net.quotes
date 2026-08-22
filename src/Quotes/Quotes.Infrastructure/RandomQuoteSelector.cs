using Quotes.Infrastructure.Abstractions;

namespace Quotes.Infrastructure;

public sealed class RandomQuoteSelector : IQuoteSelector
{
    public int NextIndex(int exclusiveUpperBound) => Random.Shared.Next(exclusiveUpperBound);
}
