using System.Globalization;
using ErrorOr;

namespace Quotes.Domain;

public sealed class QuoteAuthor : IEquatable<QuoteAuthor>
{
    public const int MinLength = 2;
    public const int MaxLength = 80;

    private QuoteAuthor(string value) => Value = value;

    public string Value { get; }

    public bool Equals(QuoteAuthor? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is QuoteAuthor other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public static bool operator ==(QuoteAuthor? left, QuoteAuthor? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(QuoteAuthor? left, QuoteAuthor? right) => !(left == right);

    public static ErrorOr<QuoteAuthor> Create(string? raw)
    {
        var normalized = QuoteText.NormalizeWhitespace(raw);

        if (normalized.Length < MinLength)
        {
            return QuoteErrors.AuthorTooShort;
        }

        if (normalized.Length > MaxLength)
        {
            return QuoteErrors.AuthorTooLong;
        }

        if (!IsValidAuthor(normalized))
        {
            return QuoteErrors.AuthorInvalidCharacters;
        }

        return new QuoteAuthor(normalized);
    }

    /// <summary>
    /// Rebuilds an author already accepted by the catalog (seed/persistence). Skips create validation.
    /// </summary>
    public static QuoteAuthor FromTrusted(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new QuoteAuthor(value);
    }

    private static bool IsValidAuthor(string author)
    {
        foreach (var ch in author)
        {
            if (char.IsLetter(ch)
                || char.IsWhiteSpace(ch)
                || ch is '-' or '\'' or '.' or '\u2019')
            {
                continue;
            }

            // Allow combining marks used in some Latin names.
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
