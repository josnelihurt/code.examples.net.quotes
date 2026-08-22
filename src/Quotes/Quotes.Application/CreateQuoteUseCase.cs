using ErrorOr;
using Quotes.Application.Abstractions;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Application;

public sealed class CreateQuoteUseCase(IQuoteRepository quotes) : ICreateQuoteUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var created = Quote.Create(command.Text, command.Author);
        if (created.IsError)
        {
            return created.Errors;
        }

        var outcome = await quotes.AddAsync(created.Value, cancellationToken);
        if (outcome is QuoteAddOutcome.DuplicateFingerprint)
        {
            return QuoteErrors.DuplicateFingerprint;
        }

        var quote = created.Value;
        return new QuoteDto(quote.Id, quote.Text.Value, quote.Author.Value);
    }
}
