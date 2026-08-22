using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>
/// Metrics leg of the list-quotes decorator chain: one <c>quotes.list.count</c>
/// increment per execution, tagged with the outcome.
/// </summary>
internal sealed class ListQuotesUseCaseTelemetry(
    IListQuotesUseCase inner) : IListQuotesUseCase
{
    public async Task<ErrorOr<QuotePageDto>> ExecuteAsync(ListQuotesQuery query, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(query, cancellationToken);
        AppMetrics.Record(
            AppMetrics.QuotesListCount,
            result.MatchFirst(
                onValue: _ => "success",
                onFirstError: error => UseCaseTelemetry.Outcome(error.Type)));
        return result;
    }
}
