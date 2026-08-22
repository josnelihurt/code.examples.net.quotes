using ErrorOr;

namespace Quotes.Application.Abstractions;

public interface ICreateQuoteUseCase
{
    Task<ErrorOr<QuoteDto>> ExecuteAsync(CreateQuoteCommand command, CancellationToken cancellationToken);
}
