using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>Logging leg of the random-quote decorator chain: entry, rejection, success.</summary>
internal sealed class GetRandomQuoteUseCaseLogging(
    IGetRandomQuoteUseCase inner,
    ILogger<GetRandomQuoteUseCaseLogging> logger) : IGetRandomQuoteUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching random quote");

        var result = await inner.ExecuteAsync(cancellationToken);
        result.SwitchFirst(
            onValue: value => logger.LogInformation("Returning quote {QuoteId}", value.Id),
            onFirstError: error => logger.LogWarning("Random quote rejected: {ErrorCode}", error.Code));
        return result;
    }
}
