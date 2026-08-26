using ErrorOr;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Quotes.Api.V3.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V3.Services;

/// <summary>
/// The v3 transport, served by ASP.NET Core's gRPC-JSON transcoding: the google.api.http
/// rules in <c>V3/Contracts/quotes.proto</c> drive the routing — no adapter endpoints exist.
/// Failures use the canonical gRPC channel: an <see cref="RpcException"/> whose status
/// carries a <c>google.rpc.Status</c> detail with the catalog errorCode, which transcoding
/// renders as <c>{"code","message","details"}</c> — deliberately different from the
/// problem+json envelope v0/v1/v2 answer with, and pinned as such by the drift tests.
/// </summary>
/// <remarks>
/// Authorization is enforced by the HTTP pipeline: the class requires authentication and
/// each method declares its own scope policy, mirroring how the v1 routes split
/// <c>quotes:read</c> from <c>quotes:write</c>.
/// </remarks>
[Authorize]
internal sealed class QuoteGrpcService(
    IGetRandomQuoteUseCase getRandomQuote,
    IGetQuoteByIdUseCase getQuoteById,
    IListQuotesUseCase listQuotes,
    ICreateQuoteUseCase createQuote) : QuoteService.QuoteServiceBase
{
    [Authorize(Policy = QuoteScopes.ReadPolicy)]
    public override async Task<Quote> GetRandomQuote(GetRandomQuoteRequest request, ServerCallContext context)
    {
        var result = await getRandomQuote.ExecuteAsync(context.CancellationToken);
        return result.Match(
            onValue: quote => quote.ToMessage(),
            onError: errors => throw ToRpcException(errors));
    }

    [Authorize(Policy = QuoteScopes.ReadPolicy)]
    public override async Task<ListQuotesResponse> ListQuotes(ListQuotesRequest request, ServerCallContext context)
    {
        // Values pass through untouched — an explicit page=0 must reach the use case and
        // answer the invalid-page rejection like every other version. `optional` presence
        // only tells absent query parameters apart from sent ones.
        var result = await listQuotes.ExecuteAsync(
            new ListQuotesQuery(
                request.HasPage ? request.Page : 1,
                request.HasPageSize ? request.PageSize : QuoteRules.DefaultPageSize),
            context.CancellationToken);
        return result.Match(
            onValue: value => value.ToMessage(),
            onError: errors => throw ToRpcException(errors));
    }

    [Authorize(Policy = QuoteScopes.ReadPolicy)]
    public override async Task<Quote> GetQuoteById(GetQuoteByIdRequest request, ServerCallContext context)
    {
        var result = await getQuoteById.ExecuteAsync(request.Id, context.CancellationToken);
        return result.Match(
            onValue: quote => quote.ToMessage(),
            onError: errors => throw ToRpcException(errors));
    }

    [Authorize(Policy = QuoteScopes.WritePolicy)]
    public override async Task<Quote> CreateQuote(CreateQuoteRequest request, ServerCallContext context)
    {
        var result = await createQuote.ExecuteAsync(
            new CreateQuoteCommand(request.Text, request.Author),
            context.CancellationToken);
        return result.Match(
            onValue: quote => quote.ToMessage(),
            onError: errors => throw ToRpcException(errors));
    }

    /// <summary>
    /// Maps ErrorOr failures onto gRPC status codes (the inverse of the HTTP mapping the
    /// shared ProblemDetails factory performs for v0/v1/v2). Transcoding renders the code
    /// and the description as <c>{"code","message","details":[]}</c>.
    /// </summary>
    /// <remarks>
    /// The catalog's machine-readable errorCode deliberately does not travel: the
    /// canonical carrier would be an <c>ErrorInfo</c> rich-error detail packed into the
    /// <c>grpc-status-details-bin</c> trailer, but this grpc-dotnet line does not parse
    /// that trailer when writing transcoded error bodies (and a packed detail makes the
    /// response writer throw — see docs/proto-transports.md). The human-readable
    /// description is the only error signal v3 clients get.
    /// </remarks>
    private static RpcException ToRpcException(List<Error> errors)
    {
        var primary = errors.Count > 0 ? errors[0] : Error.Unexpected("error.unknown", "An unexpected error occurred.");
        var statusCode = primary.Type switch
        {
            ErrorType.NotFound => StatusCode.NotFound,
            ErrorType.Conflict => StatusCode.AlreadyExists,
            ErrorType.Unauthorized => StatusCode.Unauthenticated,
            ErrorType.Forbidden => StatusCode.PermissionDenied,
            ErrorType.Unexpected => StatusCode.Internal,
            _ => StatusCode.InvalidArgument
        };

        return new RpcException(new Grpc.Core.Status(statusCode, primary.Description));
    }
}

/// <summary>proto ⇄ application mapping for the v3 contract.</summary>
internal static class QuoteMessageMapping
{
    internal static Quote ToMessage(this QuoteDto quote) => new()
    {
        Id = quote.Id,
        Text = quote.Text,
        Author = quote.Author
    };

    internal static ListQuotesResponse ToMessage(this QuotePageDto page) => new()
    {
        Items = { page.Items.Select(quote => quote.ToMessage()) },
        Page = page.Page,
        PageSize = page.PageSize,
        TotalItems = page.TotalItems,
        TotalPages = page.TotalPages
    };
}
