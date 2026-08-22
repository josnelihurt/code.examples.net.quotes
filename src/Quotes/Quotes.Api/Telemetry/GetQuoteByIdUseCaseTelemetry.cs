using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>
/// Metrics leg of the get-quote-by-id decorator chain: one <c>quotes.getbyid.count</c>
/// increment per execution, tagged with the outcome.
/// </summary>
internal sealed class GetQuoteByIdUseCaseTelemetry(
    IGetQuoteByIdUseCase inner) : IGetQuoteByIdUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(string id, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(id, cancellationToken);
        AppMetrics.Record(
            AppMetrics.QuotesGetByIdCount,
            result.MatchFirst(
                onValue: _ => "success",
                onFirstError: error => UseCaseTelemetry.Outcome(error.Type)));
        return result;
    }
}
