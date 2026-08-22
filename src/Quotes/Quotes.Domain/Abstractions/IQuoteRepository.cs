namespace Quotes.Domain.Abstractions;

public interface IQuoteRepository
{
    Quote GetRandom();
}
