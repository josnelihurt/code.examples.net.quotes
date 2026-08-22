using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
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
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        quotes.MapGet("/{id}", GetByIdAsync)
            .WithName("GetQuoteById")
            .Produces<QuoteResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        quotes.MapPost("", CreateAsync)
            .WithName("CreateQuote")
            .RequireAuthorization(JwtAuthExtensions.WriteQuotesPolicy)
            .Produces<QuoteResponseDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    internal static async Task<IResult> GetRandomAsync(
        IGetRandomQuoteUseCase useCase,
        ILoggerFactory loggerFactory,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(QuoteEndpoints));
        logger.LogInformation("Fetching random quote");

        var result = await useCase.ExecuteAsync(cancellationToken);
        if (result.IsError)
        {
            AppMetrics.Record(AppMetrics.QuotesRandomCount, "not_found");
            return result.Errors.ToProblem(http);
        }

        AppMetrics.Record(AppMetrics.QuotesRandomCount, "success");
        logger.LogInformation("Returning quote {QuoteId}", result.Value.Id);
        return Results.Ok(ToResponse(result.Value));
    }

    internal static async Task<IResult> GetByIdAsync(
        string id,
        IGetQuoteByIdUseCase useCase,
        ILoggerFactory loggerFactory,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(QuoteEndpoints));

        var result = await useCase.ExecuteAsync(id, cancellationToken);
        if (result.IsError)
        {
            logger.LogInformation("Quote {QuoteId} was not found", id);
            return result.Errors.ToProblem(http);
        }

        logger.LogInformation("Returning quote {QuoteId}", result.Value.Id);
        return Results.Ok(ToResponse(result.Value));
    }

    internal static async Task<IResult> CreateAsync(
        CreateQuoteRequestDto body,
        ICreateQuoteUseCase useCase,
        HttpContext http,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(QuoteEndpoints));
        // Author is user input: log its length, never the value itself.
        logger.LogInformation("Creating quote attributed to an author of length {AuthorLength}", body.Author.Length);

        var result = await useCase.ExecuteAsync(
            new CreateQuoteCommand(body.Text, body.Author),
            cancellationToken);
        if (result.IsError)
        {
            var outcome = result.FirstError.Type switch
            {
                ErrorType.Validation => "invalid",
                ErrorType.Conflict => "conflict",
                _ => "error"
            };
            AppMetrics.Record(AppMetrics.QuotesCreateCount, outcome);
            logger.LogWarning("Quote create rejected: {ErrorCode}", result.FirstError.Code);
            return result.Errors.ToProblem(http);
        }

        AppMetrics.Record(AppMetrics.QuotesCreateCount, "success");
        logger.LogInformation("Created quote {QuoteId}", result.Value.Id);
        return Results.Created($"/api/quotes/{result.Value.Id}", ToResponse(result.Value));
    }

    private static QuoteResponseDto ToResponse(QuoteDto quote) => new()
    {
        Id = quote.Id,
        Text = quote.Text,
        Author = quote.Author
    };
}
