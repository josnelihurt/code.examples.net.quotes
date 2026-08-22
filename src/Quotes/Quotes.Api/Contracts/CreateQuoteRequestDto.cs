using System.ComponentModel;

namespace Quotes.Api.Contracts;

[Description("Payload to add a quote to the in-memory catalog.")]
public sealed class CreateQuoteRequestDto
{
    [Description("Quote body text.")]
    public string Text { get; set; } = string.Empty;

    [Description("Attributed author of the quote.")]
    public string Author { get; set; } = string.Empty;
}
