namespace Quotes.Domain;

public sealed class QuoteFingerprint
{
    private QuoteFingerprint(string value) => Value = value;

    public string Value { get; }

    public static QuoteFingerprint FromText(QuoteText text) =>
        new(text.ComputeFingerprint());

    /// <summary>
    /// Rebuilds a fingerprint already stored by the catalog (seed/persistence).
    /// </summary>
    public static QuoteFingerprint FromTrusted(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new QuoteFingerprint(value);
    }
}
