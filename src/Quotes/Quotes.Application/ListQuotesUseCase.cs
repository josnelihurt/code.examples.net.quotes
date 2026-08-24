using ErrorOr;
using Quotes.Application.Abstractions;
using Quotes.Application.Mapping;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class ListQuotesUseCase(IQuoteRepository quotes) : IListQuotesUseCase
{
    public async Task<ErrorOr<QuotePageDto>> ExecuteAsync(ListQuotesQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (query.Page < 1 || query.Page > QuoteRules.MaxPage
            || query.PageSize < 1 || query.PageSize > QuoteRules.MaxPageSize)
        {
            return QuoteErrors.InvalidPageRequest;
        }

        // The rules above bound the product well below int.MaxValue; the long arithmetic is
        // defense in depth so a future rule change fails closed instead of wrapping the skip.
        var skip = (long)(query.Page - 1) * query.PageSize;
        if (skip > int.MaxValue)
        {
            return QuoteErrors.InvalidPageRequest;
        }

        var page = await quotes.ListAsync((int)skip, query.PageSize, cancellationToken);

        return new QuotePageDto(
            [.. page.Items.Select(item => item.ToDto())],
            query.Page,
            query.PageSize,
            page.Total,
            (int)Math.Ceiling(page.Total / (double)query.PageSize));
    }
}
