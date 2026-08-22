using ErrorOr;
using Quotes.Application.Abstractions;
using Quotes.Application.Mapping;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class GetRandomQuoteUseCase(IQuoteRepository quotes) : IGetRandomQuoteUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var quote = await quotes.GetRandomAsync(cancellationToken);
        if (quote is null)
        {
            return QuoteErrors.NotFound;
        }

        return quote.ToDto();
    }
}
