using ErrorOr;

namespace Quotes.Application.Abstractions;

public interface IGetRandomQuoteUseCase
{
    Task<ErrorOr<QuoteDto>> ExecuteAsync(CancellationToken cancellationToken);
}
