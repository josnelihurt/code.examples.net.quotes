using System.Globalization;
using System.Text;

namespace Quotes.Domain;

public sealed class Quote
{
    public const int MinTextLength = 12;
    public const int MaxTextLength = 280;
    public const int MinAuthorLength = 2;
    public const int MaxAuthorLength = 80;
    public const int MinWordCount = 3;

    private Quote(string id, string text, string author, string normalizedFingerprint)
    {
        Id = id;
        Text = text;
        Author = author;
        NormalizedFingerprint = normalizedFingerprint;
    }

    public string Id { get; }
    public string Text { get; }
    public string Author { get; }
    public string NormalizedFingerprint { get; }

    public static QuoteCreateResult Create(string? text, string? author)
    {
        var normalizedText = NormalizeWhitespace(text);
        var normalizedAuthor = NormalizeWhitespace(author);

        if (normalizedText.Length < MinTextLength)
        {
            return QuoteCreateResult.Failure(QuoteCreateError.TextTooShort);
        }

        if (normalizedText.Length > MaxTextLength)
        {
            return QuoteCreateResult.Failure(QuoteCreateError.TextTooLong);
        }

        if (CountWords(normalizedText) < MinWordCount)
        {
            return QuoteCreateResult.Failure(QuoteCreateError.TextNeedsMoreWords);
        }

        if (!EndsWithSentencePunctuation(normalizedText))
        {
            return QuoteCreateResult.Failure(QuoteCreateError.TextMustEndWithPunctuation);
        }

        if (normalizedAuthor.Length < MinAuthorLength)
        {
            return QuoteCreateResult.Failure(QuoteCreateError.AuthorTooShort);
        }

        if (normalizedAuthor.Length > MaxAuthorLength)
        {
            return QuoteCreateResult.Failure(QuoteCreateError.AuthorTooLong);
        }

        if (!IsValidAuthor(normalizedAuthor))
        {
            return QuoteCreateResult.Failure(QuoteCreateError.AuthorInvalidCharacters);
        }

        if (string.Equals(normalizedText, normalizedAuthor, StringComparison.OrdinalIgnoreCase))
        {
            return QuoteCreateResult.Failure(QuoteCreateError.AuthorEqualsText);
        }

        var fingerprint = ComputeFingerprint(normalizedText);
        var quote = new Quote(Guid.NewGuid().ToString("N"), normalizedText, normalizedAuthor, fingerprint);
        return QuoteCreateResult.Success(quote);
    }

    /// <summary>
    /// Rebuilds a quote already accepted by the catalog (seed/persistence). Skips create validation.
    /// </summary>
    public static Quote Reconstitute(string id, string text, string author, string normalizedFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedFingerprint);
        return new Quote(id, text, author, normalizedFingerprint);
    }

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

    private static string NormalizeWhitespace(string? value)
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
