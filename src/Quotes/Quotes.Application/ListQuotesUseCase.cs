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

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > QuoteRules.MaxPageSize)
        {
            return QuoteErrors.InvalidPageRequest;
        }

        var page = await quotes.ListAsync((query.Page - 1) * query.PageSize, query.PageSize, cancellationToken);

        return new QuotePageDto(
            [.. page.Items.Select(item => item.ToDto())],
            query.Page,
            query.PageSize,
            page.Total,
            (int)Math.Ceiling(page.Total / (double)query.PageSize));
    }
}
