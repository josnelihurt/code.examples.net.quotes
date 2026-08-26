using Grpc.Core;
using Quotes.Api.V2.Contracts;
using Quotes.Api.V2.Proto;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V2.Services;

/// <summary>
/// The v2 controller, implemented against the skeleton Grpc.Tools generates from
/// <c>V2/Contracts/quotes.proto</c>. The handlers speak proto messages end to end — there
/// are no hand-written DTOs in this version — and failures travel as <see cref="RpcException"/>
/// (the only error channel a generated service base offers) with every ErrorOr field
/// preserved by <see cref="GrpcErrorBridge"/>, so the HTTP adapter can rebuild the exact
/// problem body v0/v1 answer with.
/// </summary>
/// <remarks>
/// The class is deliberately a faithful gRPC service: it never touches HTTP state, which
/// keeps the door open to serving the same implementation over native gRPC later without a
/// rewrite. The v2 HTTP adapter invokes these overrides in-process.
/// </remarks>
internal sealed class QuoteGrpcService(
    IGetRandomQuoteUseCase getRandomQuote,
    IGetQuoteByIdUseCase getQuoteById,
    IListQuotesUseCase listQuotes,
    ICreateQuoteUseCase createQuote) : QuoteService.QuoteServiceBase
{
    public override async Task<Quote> GetRandomQuote(GetRandomQuoteRequest request, ServerCallContext context)
    {
        var result = await getRandomQuote.ExecuteAsync(context.CancellationToken);
        return result.Match(
            onValue: quote => quote.ToMessage(),
            onError: errors => throw GrpcErrorBridge.ToRpcException(errors));
    }

    public override async Task<ListQuotesResponse> ListQuotes(ListQuotesRequest request, ServerCallContext context)
    {
        // Values pass through untouched — including an explicit page=0 or pageSize=0, which
        // the use case rejects as quote.invalid_page_request exactly like v0/v1. Absent
        // fields arrive as proto defaults and the HTTP binding layer supplied the defaults.
        var result = await listQuotes.ExecuteAsync(
            new ListQuotesQuery(request.Page, request.PageSize),
            context.CancellationToken);
        return result.Match(
            onValue: page => page.ToMessage(),
            onError: errors => throw GrpcErrorBridge.ToRpcException(errors));
    }

    public override async Task<Quote> GetQuoteById(GetQuoteByIdRequest request, ServerCallContext context)
    {
        var result = await getQuoteById.ExecuteAsync(request.Id, context.CancellationToken);
        return result.Match(
            onValue: quote => quote.ToMessage(),
            onError: errors => throw GrpcErrorBridge.ToRpcException(errors));
    }

    public override async Task<Quote> CreateQuote(CreateQuoteRequest request, ServerCallContext context)
    {
        var result = await createQuote.ExecuteAsync(
            new CreateQuoteCommand(request.Text, request.Author),
            context.CancellationToken);
        return result.Match(
            onValue: quote => quote.ToMessage(),
            onError: errors => throw GrpcErrorBridge.ToRpcException(errors));
    }
}

/// <summary>proto ⇄ application mapping: the v2 counterpart of the V0/V1 mapping extensions.</summary>
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
