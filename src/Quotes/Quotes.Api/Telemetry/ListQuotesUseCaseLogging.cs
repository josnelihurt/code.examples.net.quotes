using ErrorOr;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

/// <summary>Logging leg of the list-quotes decorator chain: entry, rejection, success.</summary>
internal sealed class ListQuotesUseCaseLogging(
    IListQuotesUseCase inner,
    ILogger<ListQuotesUseCaseLogging> logger) : IListQuotesUseCase
{
    public async Task<ErrorOr<QuotePageDto>> ExecuteAsync(ListQuotesQuery query, CancellationToken cancellationToken)
    {
        logger.LogInformation("Listing quotes page {Page} with page size {PageSize}", query.Page, query.PageSize);

        var result = await inner.ExecuteAsync(query, cancellationToken);
        result.SwitchFirst(
            onValue: value => logger.LogInformation(
                "Returning {ItemCount} of {TotalItems} quotes", value.Items.Count, value.TotalItems),
            onFirstError: error => logger.LogWarning("Quote listing rejected: {ErrorCode}", error.Code));
        return result;
    }
}
