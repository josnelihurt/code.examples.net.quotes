using ErrorOr;

namespace Quotes.Application.Abstractions;

public interface IListQuotesUseCase
{
    Task<ErrorOr<QuotePageDto>> ExecuteAsync(ListQuotesQuery query, CancellationToken cancellationToken);
}
