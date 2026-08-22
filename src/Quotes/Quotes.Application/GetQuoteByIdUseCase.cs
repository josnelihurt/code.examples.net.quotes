using ErrorOr;
using Quotes.Application.Abstractions;
using Quotes.Application.Mapping;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class GetQuoteByIdUseCase(IQuoteRepository quotes) : IGetQuoteByIdUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return QuoteErrors.NotFound;
        }

        var quote = await quotes.GetByIdAsync(id, cancellationToken);
        if (quote is null)
        {
            return QuoteErrors.NotFound;
        }

        return quote.ToDto();
    }
}
