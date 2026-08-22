using Quotes.Api.V1.Contracts;
using Quotes.Api.V1.Mapping;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V1.Endpoints;

public static class QuoteEndpoints
{
    internal const string GetByIdRouteName = "GetQuoteById";

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

        quotes.MapGet("", ListAsync)
            .WithName("ListQuotes")
            .RequireAuthorization(JwtAuthExtensions.ReadQuotesPolicy)
            .Produces<QuotePageResponseDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        quotes.MapGet("/{id}", GetByIdAsync)
            .WithName(GetByIdRouteName)
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
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(cancellationToken);
        return result.Match(
            onValue: value => Results.Ok(value.ToResponse()),
            onError: errors => errors.ToProblem(http));
    }

    internal static async Task<IResult> ListAsync(
        IListQuotesUseCase useCase,
        HttpContext http,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = QuoteRules.DefaultPageSize)
    {
        var result = await useCase.ExecuteAsync(new ListQuotesQuery(page, pageSize), cancellationToken);
        return result.Match(
            onValue: value => Results.Ok(value.ToResponse()),
            onError: errors => errors.ToProblem(http));
    }

    internal static async Task<IResult> GetByIdAsync(
        string id,
        IGetQuoteByIdUseCase useCase,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(id, cancellationToken);
        return result.Match(
            onValue: value => Results.Ok(value.ToResponse()),
            onError: errors => errors.ToProblem(http));
    }

    internal static async Task<IResult> CreateAsync(
        CreateQuoteRequestDto body,
        ICreateQuoteUseCase useCase,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(body.ToCommand(), cancellationToken);
        return result.Match(
            onValue: value => Results.CreatedAtRoute(
                GetByIdRouteName,
                new { id = value.Id },
                value.ToResponse()),
            onError: errors => errors.ToProblem(http));
    }
}
