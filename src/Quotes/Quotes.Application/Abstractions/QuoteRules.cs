using Quotes.Domain;

namespace Quotes.Application.Abstractions;

/// <summary>
/// Read-only view of the domain's catalog limits so outer layers (e.g. transport-level
/// request validation) can size their guards without referencing the domain project.
/// </summary>
public static class QuoteRules
{
    public const int MinTextLength = Quote.MinTextLength;
    public const int MaxTextLength = Quote.MaxTextLength;
    public const int MinAuthorLength = Quote.MinAuthorLength;
    public const int MaxAuthorLength = Quote.MaxAuthorLength;
    public const int MinWordCount = Quote.MinWordCount;
}
