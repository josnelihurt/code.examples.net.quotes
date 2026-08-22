using ErrorOr;

namespace Quotes.Application.Abstractions;

public interface IGetQuoteByIdUseCase
{
    Task<ErrorOr<QuoteDto>> ExecuteAsync(string id, CancellationToken cancellationToken);
}
