using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V1.Contracts;

/// <example>{"text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}</example>
[Description("Payload to add a quote to the catalog.")]
public sealed class CreateQuoteRequestDto
{
    [Description("Quote body text.")]
    [Required]
    [MaxLength(QuoteRules.MaxTextLength)]
    public string Text { get; set; } = string.Empty;

    [Description("Attributed author of the quote.")]
    [Required]
    [MaxLength(QuoteRules.MaxAuthorLength)]
    public string Author { get; set; } = string.Empty;
}
