using Quotes.Domain;

namespace Quotes.Application.Abstractions;

/// <summary>
/// Read-only view of the domain's catalog limits so outer layers (e.g. transport-level
/// request validation) can size their guards without referencing the domain project.
/// </summary>
public static class QuoteRules
{
    public const int MinTextLength = QuoteText.MinLength;
    public const int MaxTextLength = QuoteText.MaxLength;
    public const int MinAuthorLength = QuoteAuthor.MinLength;
    public const int MaxAuthorLength = QuoteAuthor.MaxLength;
    public const int MinWordCount = QuoteText.MinWordCount;
}
