using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>
/// Metrics leg of the random-quote decorator chain: one <c>quotes.random.count</c>
/// increment per execution, tagged with the outcome.
/// </summary>
internal sealed class GetRandomQuoteUseCaseTelemetry(
    IGetRandomQuoteUseCase inner) : IGetRandomQuoteUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(cancellationToken);
        AppMetrics.Record(
            AppMetrics.QuotesRandomCount,
            result.MatchFirst(
                onValue: _ => "success",
                onFirstError: error => UseCaseTelemetry.Outcome(error.Type)));
        return result;
    }
}
