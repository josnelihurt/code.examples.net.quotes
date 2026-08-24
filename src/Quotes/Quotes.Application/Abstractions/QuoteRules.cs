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

    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Upper bound for <c>page</c>. The guard exists so the 1-based → offset translation
    /// (<c>(page - 1) * pageSize</c>) can never overflow <see langword="int"/> and turn a
    /// bad request into an unhandled exception.
    /// </summary>
    public const int MaxPage = 10_000;
}
