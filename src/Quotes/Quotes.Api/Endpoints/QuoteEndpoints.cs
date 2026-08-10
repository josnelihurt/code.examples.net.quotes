using AspireQuotesPoc.Http;
using AspireQuotesPoc.Telemetry;
using Quotes.Api.Contracts;
using Quotes.Application;

namespace Quotes.Api.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/quotes/random", GetRandomAsync)
            .WithName("GetRandomQuote")
            .WithTags("Quotes")
            .Produces<QuoteResponseDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponseDto>(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    internal static async Task<IResult> GetRandomAsync(
        HttpContext http,
        IGetRandomQuoteUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(QuoteEndpoints));
        if (!BearerToken.TryParse(http.Request.Headers.Authorization.FirstOrDefault(), out var token))
        {
            AppMetrics.Record(AppMetrics.QuotesRandomCount, "failure");
            logger.LogWarning("Missing bearer token on random quote request");
            return Unauthorized();
        }

        var correlationId = http.GetCorrelationId();
        logger.LogInformation("Fetching random quote");

        var quote = await useCase.ExecuteAsync(token, correlationId, cancellationToken);
        if (quote is null)
        {
            AppMetrics.Record(AppMetrics.QuotesRandomCount, "failure");
            logger.LogWarning("Auth validation failed for random quote request");
            return Unauthorized();
        }

        AppMetrics.Record(AppMetrics.QuotesRandomCount, "success");
        logger.LogInformation("Returning quote {QuoteId}", quote.Id);
        return Results.Ok(new QuoteResponseDto
        {
            Id = quote.Id,
            Text = quote.Text,
            Author = quote.Author
        });
    }

    private static IResult Unauthorized() => Results.Json(
        new ErrorResponseDto { Error = "Unauthorized" },
        statusCode: StatusCodes.Status401Unauthorized);
}
