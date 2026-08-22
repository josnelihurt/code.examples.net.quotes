using ErrorOr;

namespace Quotes.Domain;

public sealed class Quote
{
    private Quote(string id, QuoteText text, QuoteAuthor author, QuoteFingerprint fingerprint)
    {
        Id = id;
        Text = text;
        Author = author;
        Fingerprint = fingerprint;
    }

    public string Id { get; }
    public QuoteText Text { get; }
    public QuoteAuthor Author { get; }
    public QuoteFingerprint Fingerprint { get; }

    public static ErrorOr<Quote> Create(string? text, string? author)
    {
        var textResult = QuoteText.Create(text);
        if (textResult.IsError)
        {
            return textResult.Errors;
        }

        var authorResult = QuoteAuthor.Create(author);
        if (authorResult.IsError)
        {
            return authorResult.Errors;
        }

        if (string.Equals(textResult.Value.Value, authorResult.Value.Value, StringComparison.OrdinalIgnoreCase))
        {
            return QuoteErrors.AuthorEqualsText;
        }

        var fingerprint = QuoteFingerprint.FromText(textResult.Value);
        return new Quote(Guid.NewGuid().ToString("N"), textResult.Value, authorResult.Value, fingerprint);
    }

    /// <summary>
    /// Rebuilds a quote already accepted by the catalog (seed/persistence). Skips create validation.
    /// </summary>
    public static Quote Reconstitute(string id, string text, string author, string normalizedFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new Quote(
            id,
            QuoteText.FromTrusted(text),
            QuoteAuthor.FromTrusted(author),
            QuoteFingerprint.FromTrusted(normalizedFingerprint));
    }
}
