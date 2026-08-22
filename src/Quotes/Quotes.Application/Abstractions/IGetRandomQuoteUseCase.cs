namespace Quotes.Application.Abstractions;

public interface IGetRandomQuoteUseCase
{
    Task<QuoteDto> ExecuteAsync(CancellationToken cancellationToken);
}
