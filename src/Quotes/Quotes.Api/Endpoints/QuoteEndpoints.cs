using AspireQuotesPoc.Telemetry;
using Quotes.Api.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var quotes = endpoints.MapGroup("/api/quotes")
            .RequireAuthorization()
            .WithTags("Quotes");

        quotes.MapGet("/random", GetRandomAsync)
            .WithName("GetRandomQuote")
            .Produces<QuoteResponseDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponseDto>(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    internal static async Task<IResult> GetRandomAsync(
        IGetRandomQuoteUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(QuoteEndpoints));
        logger.LogInformation("Fetching random quote");

        var quote = await useCase.ExecuteAsync(cancellationToken);

        AppMetrics.Record(AppMetrics.QuotesRandomCount, "success");
        logger.LogInformation("Returning quote {QuoteId}", quote.Id);
        return Results.Ok(new QuoteResponseDto
        {
            Id = quote.Id,
            Text = quote.Text,
            Author = quote.Author
        });
    }
}
