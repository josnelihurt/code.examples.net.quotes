using ErrorOr;
using Quotes.Application.Abstractions;
using Quotes.Application.Mapping;
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

        return created.Value.ToDto();
    }
}
