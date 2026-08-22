using System.Text;
using ErrorOr;

namespace Quotes.Domain;

public sealed class QuoteText : IEquatable<QuoteText>
{
    public const int MinLength = 12;
    public const int MaxLength = 280;
    public const int MinWordCount = 3;

    private QuoteText(string value) => Value = value;

    public string Value { get; }

    public bool Equals(QuoteText? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is QuoteText other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(QuoteText? left, QuoteText? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(QuoteText? left, QuoteText? right) => !(left == right);

    public static ErrorOr<QuoteText> Create(string? raw)
    {
        var normalized = NormalizeWhitespace(raw);

        if (normalized.Length < MinLength)
        {
            return QuoteErrors.TextTooShort;
        }

        if (normalized.Length > MaxLength)
        {
            return QuoteErrors.TextTooLong;
        }

        if (CountWords(normalized) < MinWordCount)
        {
            return QuoteErrors.TextNeedsMoreWords;
        }

        if (!EndsWithSentencePunctuation(normalized))
        {
            return QuoteErrors.TextMustEndWithPunctuation;
        }

        return new QuoteText(normalized);
    }

    /// <summary>
    /// Rebuilds text already accepted by the catalog (seed/persistence). Skips create validation.
    /// </summary>
    public static QuoteText FromTrusted(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new QuoteText(value);
    }

    public string ComputeFingerprint() => ComputeFingerprint(Value);

    /// <summary>
    /// Fingerprint for raw input (e.g. seed helpers) without going through full create validation.
    /// </summary>
    public static string ComputeFingerprint(string text)
    {
        var normalized = NormalizeWhitespace(text).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(ch);
                pendingSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                pendingSpace = true;
            }
            else
            {
                // Drop punctuation but keep a word break so "First,solve" fingerprints as "first solve".
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    internal static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static int CountWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    private static bool EndsWithSentencePunctuation(string text)
    {
        var last = text[^1];
        return last is '.' or '!' or '?';
    }
}
