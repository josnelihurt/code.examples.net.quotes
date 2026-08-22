using AspireQuotesPoc.Telemetry;
using Quotes.Api.Contracts;
using Quotes.Application.Abstractions;
using Quotes.Domain;

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

        quotes.MapPost("/", CreateAsync)
            .WithName("CreateQuote")
            .Produces<QuoteResponseDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponseDto>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponseDto>(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

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

    internal static async Task<IResult> CreateAsync(
        CreateQuoteRequestDto body,
        ICreateQuoteUseCase useCase,
        HttpContext http,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(nameof(QuoteEndpoints));
        var validation = await ValidationFilter.ValidateAsync(body, http);
        if (validation is not null)
        {
            return validation;
        }

        logger.LogInformation("Creating quote attributed to {Author}", body.Author);

        var result = await useCase.ExecuteAsync(
            new CreateQuoteCommand(body.Text, body.Author),
            cancellationToken);

        return result.Status switch
        {
            CreateQuoteStatus.Created when result.Quote is not null => Created(result.Quote, logger),
            CreateQuoteStatus.Invalid => Invalid(result.Error, logger),
            CreateQuoteStatus.Conflict => Conflict(logger),
            _ => Unexpected(result.Status, logger)
        };
    }

    private static IResult Created(QuoteDto quote, ILogger logger)
    {
        AppMetrics.Record(AppMetrics.QuotesCreateCount, "success");
        logger.LogInformation("Created quote {QuoteId}", quote.Id);
        return Results.Created($"/api/quotes/{quote.Id}", new QuoteResponseDto
        {
            Id = quote.Id,
            Text = quote.Text,
            Author = quote.Author
        });
    }

    private static IResult Invalid(QuoteCreateError? error, ILogger logger)
    {
        AppMetrics.Record(AppMetrics.QuotesCreateCount, "invalid");
        var message = Describe(error);
        logger.LogWarning("Quote create rejected: {Reason}", message);
        return Results.Json(
            new ErrorResponseDto { Error = message },
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult Conflict(ILogger logger)
    {
        AppMetrics.Record(AppMetrics.QuotesCreateCount, "conflict");
        logger.LogWarning("Quote create conflicted with an existing fingerprint");
        return Results.Json(
            new ErrorResponseDto { Error = "A quote with the same meaning already exists." },
            statusCode: StatusCodes.Status409Conflict);
    }

    private static IResult Unexpected(CreateQuoteStatus status, ILogger logger)
    {
        AppMetrics.Record(AppMetrics.QuotesCreateCount, "error");
        logger.LogError("Quote create returned unexpected status {Status}", status);
        return Results.Json(
            new ErrorResponseDto { Error = "Unable to create quote." },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    private static string Describe(QuoteCreateError? error) => error switch
    {
        QuoteCreateError.TextTooShort => $"Quote text must be at least {Quote.MinTextLength} characters.",
        QuoteCreateError.TextTooLong => $"Quote text must be at most {Quote.MaxTextLength} characters.",
        QuoteCreateError.TextNeedsMoreWords => $"Quote text must contain at least {Quote.MinWordCount} words.",
        QuoteCreateError.TextMustEndWithPunctuation => "Quote text must end with '.', '!', or '?'.",
        QuoteCreateError.AuthorTooShort => $"Author must be at least {Quote.MinAuthorLength} characters.",
        QuoteCreateError.AuthorTooLong => $"Author must be at most {Quote.MaxAuthorLength} characters.",
        QuoteCreateError.AuthorInvalidCharacters => "Author may only contain letters, spaces, hyphens, apostrophes, and periods.",
        QuoteCreateError.AuthorEqualsText => "Author must not be the same as the quote text.",
        _ => "Quote is invalid."
    };
}
