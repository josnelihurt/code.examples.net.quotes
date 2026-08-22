namespace Quotes.Domain;

public sealed class QuoteFingerprint : IEquatable<QuoteFingerprint>
{
    private QuoteFingerprint(string value) => Value = value;

    public string Value { get; }

    public bool Equals(QuoteFingerprint? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is QuoteFingerprint other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(QuoteFingerprint? left, QuoteFingerprint? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(QuoteFingerprint? left, QuoteFingerprint? right) => !(left == right);

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
