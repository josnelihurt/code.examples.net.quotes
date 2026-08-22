using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>Logging leg of the get-quote-by-id decorator chain: entry, rejection, success.</summary>
internal sealed class GetQuoteByIdUseCaseLogging(
    IGetQuoteByIdUseCase inner,
    ILogger<GetQuoteByIdUseCaseLogging> logger) : IGetQuoteByIdUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(string id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching quote {QuoteId}", id);

        var result = await inner.ExecuteAsync(id, cancellationToken);
        result.SwitchFirst(
            onValue: value => logger.LogInformation("Returning quote {QuoteId}", value.Id),
            onFirstError: error => logger.LogWarning("Quote lookup rejected: {ErrorCode}", error.Code));
        return result;
    }
}
