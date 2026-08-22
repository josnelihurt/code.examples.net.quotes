using System.ComponentModel;

namespace Quotes.Api.V1.Contracts;

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
