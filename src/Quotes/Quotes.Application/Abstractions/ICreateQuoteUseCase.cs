namespace Quotes.Application.Abstractions;

public interface ICreateQuoteUseCase
{
    Task<CreateQuoteResult> ExecuteAsync(CreateQuoteCommand command, CancellationToken cancellationToken);
}
