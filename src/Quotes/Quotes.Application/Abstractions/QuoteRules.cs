using Quotes.Domain;

namespace Quotes.Application.Abstractions;

/// <summary>
/// Read-only view of the domain's catalog limits so outer layers (e.g. transport-level
/// request validation) can size their guards without referencing the domain project.
/// </summary>
public static class QuoteRules
{
    public static int MinTextLength => Quote.MinTextLength;
    public static int MaxTextLength => Quote.MaxTextLength;
    public static int MinAuthorLength => Quote.MinAuthorLength;
    public static int MaxAuthorLength => Quote.MaxAuthorLength;
    public static int MinWordCount => Quote.MinWordCount;
}
