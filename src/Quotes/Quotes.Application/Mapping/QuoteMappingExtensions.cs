using Quotes.Application.Abstractions;
using Quotes.Domain;

namespace Quotes.Application.Mapping;

public static class QuoteMappingExtensions
{
    public static QuoteDto ToDto(this Quote quote) =>
        new(quote.Id, quote.Text.Value, quote.Author.Value);
}
