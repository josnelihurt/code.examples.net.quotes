namespace Quotes.Domain;

public sealed class QuoteCreateResult
{
    private QuoteCreateResult(Quote? quote, QuoteCreateError? error)
    {
        Quote = quote;
        Error = error;
    }

    public Quote? Quote { get; }
    public QuoteCreateError? Error { get; }
    public bool Succeeded => Quote is not null;

    public static QuoteCreateResult Success(Quote quote) => new(quote, null);

    public static QuoteCreateResult Failure(QuoteCreateError error) => new(null, error);
}
