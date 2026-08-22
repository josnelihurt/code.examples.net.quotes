using Quotes.Application.Abstractions;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class CreateQuoteUseCase(IQuoteRepository quotes) : ICreateQuoteUseCase
{
    private readonly IQuoteRepository _quotes = quotes;

    public Task<CreateQuoteResult> ExecuteAsync(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var created = Quote.Create(command.Text, command.Author);
        if (!created.Succeeded || created.Quote is null)
        {
            return Task.FromResult(new CreateQuoteResult(
                CreateQuoteStatus.Invalid,
                Error: created.Error));
        }

        var quote = created.Quote;
        if (_quotes.ExistsByFingerprint(quote.NormalizedFingerprint))
        {
            return Task.FromResult(new CreateQuoteResult(CreateQuoteStatus.Conflict));
        }

        _quotes.Add(quote);
        return Task.FromResult(new CreateQuoteResult(
            CreateQuoteStatus.Created,
            new QuoteDto(quote.Id, quote.Text, quote.Author)));
    }
}
