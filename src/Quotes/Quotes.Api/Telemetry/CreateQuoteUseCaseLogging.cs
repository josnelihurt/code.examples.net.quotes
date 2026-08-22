using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>Logging leg of the create-quote decorator chain: entry, rejection, success.</summary>
internal sealed class CreateQuoteUseCaseLogging(
    ICreateQuoteUseCase inner,
    ILogger<CreateQuoteUseCaseLogging> logger) : ICreateQuoteUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        // Author is user input: log its length, never the value itself.
        logger.LogInformation("Creating quote attributed to an author of length {AuthorLength}", command.Author.Length);

        var result = await inner.ExecuteAsync(command, cancellationToken);
        result.SwitchFirst(
            onValue: value => logger.LogInformation("Created quote {QuoteId}", value.Id),
            onFirstError: error => logger.LogWarning("Quote create rejected: {ErrorCode}", error.Code));
        return result;
    }
}
