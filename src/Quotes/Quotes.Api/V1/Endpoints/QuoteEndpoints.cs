using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
using Quotes.Api.V1.Contracts;
using Quotes.Api.V1.Mapping;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V1.Endpoints;

/// <summary>Logger category for quote endpoint handlers (static types cannot be used as ILogger&lt;T&gt; arguments).</summary>
internal sealed class QuoteEndpointsLog;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var quotes = endpoints.MapGroup("/api/v1/quotes")
            .RequireAuthorization()
            .WithTags("Quotes v1")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        quotes.MapGet("/random", GetRandomAsync)
            .WithName("GetRandomQuote")
            .RequireAuthorization(JwtAuthExtensions.ReadQuotesPolicy)
            .Produces<QuoteResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        quotes.MapGet("/{id}", GetByIdAsync)
            .WithName("GetQuoteById")
            .RequireAuthorization(JwtAuthExtensions.ReadQuotesPolicy)
            .Produces<QuoteResponseDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        quotes.MapPost("", CreateAsync)
            .WithName("CreateQuote")
            .RequireAuthorization(JwtAuthExtensions.WriteQuotesPolicy)
            .Produces<QuoteResponseDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    internal static async Task<IResult> GetRandomAsync(
        IGetRandomQuoteUseCase useCase,
        ILogger<QuoteEndpointsLog> logger,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching random quote");

        var result = await useCase.ExecuteAsync(cancellationToken);
        if (result.IsError)
        {
            AppMetrics.Record(AppMetrics.QuotesRandomCount, "not_found");
            return result.Errors.ToProblem(http);
        }

        AppMetrics.Record(AppMetrics.QuotesRandomCount, "success");
        logger.LogInformation("Returning quote {QuoteId}", result.Value.Id);
        return Results.Ok(result.Value.ToResponse());
    }

    internal static async Task<IResult> GetByIdAsync(
        string id,
        IGetQuoteByIdUseCase useCase,
        ILogger<QuoteEndpointsLog> logger,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, cancellationToken);
        if (result.IsError)
        {
            logger.LogInformation("Quote {QuoteId} was not found", id);
            return result.Errors.ToProblem(http);
        }

        logger.LogInformation("Returning quote {QuoteId}", result.Value.Id);
        return Results.Ok(result.Value.ToResponse());
    }

    internal static async Task<IResult> CreateAsync(
        CreateQuoteRequestDto body,
        ICreateQuoteUseCase useCase,
        HttpContext http,
        ILogger<QuoteEndpointsLog> logger,
        CancellationToken cancellationToken)
    {
        // Author is user input: log its length, never the value itself.
        logger.LogInformation("Creating quote attributed to an author of length {AuthorLength}", body.Author.Length);

        var result = await useCase.ExecuteAsync(body.ToCommand(), cancellationToken);
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
        return Results.CreatedAtRoute(
            "GetQuoteById",
            new { id = result.Value.Id },
            result.Value.ToResponse());
    }
}
