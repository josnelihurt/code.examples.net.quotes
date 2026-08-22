using ErrorOr;
using Quotes.Application.Abstractions;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class GetQuoteByIdUseCase(IQuoteRepository quotes) : IGetQuoteByIdUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var quote = await quotes.GetByIdAsync(id, cancellationToken);
        if (quote is null)
        {
            return QuoteErrors.NotFound;
        }

        return new QuoteDto(quote.Id, quote.Text, quote.Author);
    }
}
