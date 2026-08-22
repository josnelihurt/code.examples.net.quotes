using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V0.Contracts;

/// <example>{"text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}</example>
/// <remarks>
/// Deliberately a separate type from its v1 twin. Versions own their contracts so one can change
/// without dragging the other along; sharing the DTO would couple the two versions permanently.
/// </remarks>
[Description("Payload to add a quote to the in-memory catalog.")]
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
