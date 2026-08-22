using System.ComponentModel;

namespace Quotes.Api.V0.Contracts;

/// <example>{"id":"3f2b8a9c1d4e5f6a7b8c9d0e1f2a3b4c","text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}</example>
[Description("Quote returned to an authenticated client.")]
public sealed class QuoteResponseDto
{
    [Description("Stable quote identifier.")]
    public required string Id { get; init; }

    [Description("Quote body text.")]
    public required string Text { get; init; }

    [Description("Attributed author of the quote.")]
    public required string Author { get; init; }
}
