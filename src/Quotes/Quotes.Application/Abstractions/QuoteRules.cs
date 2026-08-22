using Quotes.Domain;

namespace Quotes.Application.Abstractions;

/// <summary>
/// Single source of truth for transport-level guards: outer layers size their Data
/// Annotation limits from these constants instead of duplicating magic numbers.
/// </summary>
public static class QuoteRules
{
    public const int MinTextLength = QuoteText.MinLength;
    public const int MaxTextLength = QuoteText.MaxLength;
    public const int MinAuthorLength = QuoteAuthor.MinLength;
    public const int MaxAuthorLength = QuoteAuthor.MaxLength;
    public const int MinWordCount = QuoteText.MinWordCount;
}
