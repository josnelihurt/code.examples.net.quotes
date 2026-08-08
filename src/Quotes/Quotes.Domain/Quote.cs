namespace Quotes.Domain;

public sealed class Quote
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string Author { get; init; }
}

public interface IQuoteRepository
{
    Quote GetRandom();
}
