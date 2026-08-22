using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>
/// Metrics leg of the create-quote decorator chain: one <c>quotes.create.count</c>
/// increment per execution, tagged with the outcome.
/// </summary>
internal sealed class CreateQuoteUseCaseTelemetry(
    ICreateQuoteUseCase inner) : ICreateQuoteUseCase
{
    public async Task<ErrorOr<QuoteDto>> ExecuteAsync(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(command, cancellationToken);
        AppMetrics.Record(
            AppMetrics.QuotesCreateCount,
            result.MatchFirst(
                onValue: _ => "success",
                onFirstError: error => UseCaseTelemetry.Outcome(error.Type)));
        return result;
    }
}
