using Quotes.Api.V0.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V0.Mapping;

/// <summary>
/// Transport transformers for v0: request DTO in, application command out; application DTO in,
/// response DTO out. Written by hand rather than reflected so the mapping is greppable and the
/// compiler catches contract drift.
/// </summary>
public static class QuoteMappingExtensions
{
    public static CreateQuoteCommand ToCommand(this CreateQuoteRequestDto body) =>
        new(body.Text, body.Author);

    public static QuoteResponseDto ToResponse(this QuoteDto quote) => new()
    {
        Id = quote.Id,
        Text = quote.Text,
        Author = quote.Author
    };

    public static QuotePageResponseDto ToResponse(this QuotePageDto page) => new()
    {
        Items = [.. page.Items.Select(quote => quote.ToResponse())],
        Page = page.Page,
        PageSize = page.PageSize,
        TotalItems = page.TotalItems,
        TotalPages = page.TotalPages
    };
}
