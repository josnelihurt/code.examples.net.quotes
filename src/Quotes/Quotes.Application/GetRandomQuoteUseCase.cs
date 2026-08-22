using Quotes.Application.Abstractions;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class GetRandomQuoteUseCase(IQuoteRepository quotes) : IGetRandomQuoteUseCase
{
    private readonly IQuoteRepository _quotes = quotes;

    public Task<QuoteDto> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var quote = _quotes.GetRandom();
        return Task.FromResult(new QuoteDto(quote.Id, quote.Text, quote.Author));
    }
}
