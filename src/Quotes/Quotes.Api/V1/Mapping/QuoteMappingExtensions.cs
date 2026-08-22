using Quotes.Api.V1.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V1.Mapping;

public static class QuoteMappingExtensions
{
    public static QuoteResponseDto ToResponse(this QuoteDto quote) => new()
    {
        Id = quote.Id,
        Text = quote.Text,
        Author = quote.Author
    };
}
