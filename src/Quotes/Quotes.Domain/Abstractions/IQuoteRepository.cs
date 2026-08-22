namespace Quotes.Domain.Abstractions;

public interface IQuoteRepository
{
    Quote GetRandom();
    bool ExistsByFingerprint(string normalizedFingerprint);
    void Add(Quote quote);
}
