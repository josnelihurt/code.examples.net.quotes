using AspireQuotesPoc.ServiceDefaults.Errors;
using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Google.Protobuf;
using Grpc.Core;
using Quotes.Api.V2.Contracts;
using Quotes.Api.V2.Proto;
using Quotes.Api.V2.Services;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V2.Endpoints;

/// <summary>
/// The HTTP half of the v2 transport: a thin adapter that binds JSON onto the proto messages
/// Grpc.Tools generates, invokes <see cref="QuoteGrpcService"/> in-process through the
/// contract's google.api.http rules, and renders replies and failures exactly like v0/v1 —
/// same bodies, same problem+json envelope, same 201 + Location. The routing below mirrors
/// the annotations in <c>V2/Contracts/quotes.proto</c>; a conformance test fails if the two
/// drift apart.
/// </summary>
public static class QuoteEndpoints
{
    /// <summary>OpenAPI document this version publishes into. See <c>AddStandardApiServices</c>.</summary>
    internal const string DocumentName = "v2";

    // Route names are version-local on purpose: Location headers must stay inside v2 (v1
    // took the un-suffixed names first; v0 learned this the hard way).
    internal const string GetRandomQuoteRouteName = "GetRandomQuoteV2";
    internal const string ListQuotesRouteName = "ListQuotesV2";
    internal const string GetByIdRouteName = "GetQuoteByIdV2";
    internal const string CreateQuoteRouteName = "CreateQuoteV2";

    private const string _forbiddenDetail =
        "The access token is missing the required scope (quotes:read or quotes:write).";

    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var quotes = endpoints.MapGroup("/api/v2/quotes")
            .RequireAuthorization()
            .WithGroupName(DocumentName)
            .WithTags("Quotes v2")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithProblemExample(
                StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "A valid bearer token is required.",
                errorCode: JwtAuthExtensions.TokenInvalidErrorCode);

        quotes.MapGet("/random", GetRandomAsync)
            .WithName(GetRandomQuoteRouteName)
            .RequireAuthorization(QuoteScopes.ReadPolicy)
            .Produces<Quote>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithProblemExample(StatusCodes.Status403Forbidden, title: "Forbidden", detail: _forbiddenDetail)
            .WithProblemExample(StatusCodes.Status404NotFound, "quote.not_found", "Quote not found.");

        quotes.MapGet("", ListAsync)
            .WithName(ListQuotesRouteName)
            .RequireAuthorization(QuoteScopes.ReadPolicy)
            .Produces<ListQuotesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithProblemExample(
                StatusCodes.Status400BadRequest,
                "quote.invalid_page_request",
                "The requested page or page size is outside the allowed range.")
            .WithProblemExample(StatusCodes.Status403Forbidden, title: "Forbidden", detail: _forbiddenDetail);

        quotes.MapGet("/{id}", GetByIdAsync)
            .WithName(GetByIdRouteName)
            .RequireAuthorization(QuoteScopes.ReadPolicy)
            .Produces<Quote>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithProblemExample(StatusCodes.Status403Forbidden, title: "Forbidden", detail: _forbiddenDetail)
            .WithProblemExample(StatusCodes.Status404NotFound, "quote.not_found", "Quote not found.");

        quotes.MapPost("", CreateAsync)
            .WithName(CreateQuoteRouteName)
            .RequireAuthorization(QuoteScopes.WritePolicy)
            .Accepts<CreateQuoteRequest>("application/json")
            .Produces<Quote>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithProblemExample(
                StatusCodes.Status400BadRequest,
                "quote.text_too_short",
                "Quote text must be at least 12 characters.")
            .WithProblemExample(StatusCodes.Status403Forbidden, title: "Forbidden", detail: _forbiddenDetail)
            .WithProblemExample(
                StatusCodes.Status409Conflict,
                "quote.duplicate_fingerprint",
                "A quote with the same meaning already exists.");

        return endpoints;
    }

    /// <summary>Returns a random quote from the catalog.</summary>
    /// <remarks>
    /// Requires a bearer JWT issued by the Auth API with the <c>quotes:read</c> scope
    /// (<c>POST /api/v1/auth/login</c>); a valid token without the scope answers 403. The
    /// catalog boots seeded (eight quotes), so 404 here means the catalog was emptied, not
    /// that it has not been filled yet. Send <c>X-Correlation-Id</c> to correlate calls; it
    /// is echoed on every response and embedded in problem details.
    /// </remarks>
    /// <response code="200">A random quote.</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:read</c> scope.</response>
    /// <response code="404">The catalog is empty (errorCode <c>quote.not_found</c>).</response>
    internal static async Task<IResult> GetRandomAsync(
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var service = http.RequestServices.GetRequiredService<QuoteGrpcService>();
        Quote quote;
        try
        {
            quote = await service.GetRandomQuote(
                new GetRandomQuoteRequest(),
                new AdapterServerCallContext("/quotes.v2.QuoteService/GetRandomQuote", cancellationToken));
        }
        catch (RpcException ex)
        {
            return GrpcErrorBridge.ToErrors(ex).ToProblem(http);
        }

        return new ProtoJsonResult(StatusCodes.Status200OK, null, ProtoJson.Format(quote));
    }

    /// <summary>Lists one page of the quote catalog in stable order.</summary>
    /// <remarks>
    /// Requires a bearer JWT issued by the Auth API with the <c>quotes:read</c> scope
    /// (<c>POST /api/v1/auth/login</c>); a valid token without the scope answers 403.
    /// Pagination is 1-based: <c>page</c> starts at 1 and <c>pageSize</c> is capped at 100
    /// (default 20); values outside those ranges answer 400 with
    /// <c>quote.invalid_page_request</c>. Pages beyond the last return an empty
    /// <c>items</c> array, not an error. Send <c>X-Correlation-Id</c> to correlate calls;
    /// it is echoed on every response and embedded in problem details.
    /// </remarks>
    /// <param name="page" example="2">1-based page number (minimum 1).</param>
    /// <param name="pageSize" example="20">Items per page, between 1 and 100 (default 20).</param>
    /// <param name="http">Request context, not part of the HTTP contract.</param>
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <response code="200">The requested page as <c>{ items, page, pageSize, totalItems, totalPages }</c>.</response>
    /// <response code="400">Page or pageSize outside the allowed range (errorCode <c>quote.invalid_page_request</c>).</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:read</c> scope.</response>
    internal static async Task<IResult> ListAsync(
        HttpContext http,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = QuoteRules.DefaultPageSize)
    {
        var service = http.RequestServices.GetRequiredService<QuoteGrpcService>();
        ListQuotesResponse result;
        try
        {
            result = await service.ListQuotes(
                new ListQuotesRequest { Page = page, PageSize = pageSize },
                new AdapterServerCallContext("/quotes.v2.QuoteService/ListQuotes", cancellationToken));
        }
        catch (RpcException ex)
        {
            return GrpcErrorBridge.ToErrors(ex).ToProblem(http);
        }

        return new ProtoJsonResult(StatusCodes.Status200OK, null, ProtoJson.Format(result));
    }

    /// <summary>Returns one quote by id.</summary>
    /// <remarks>
    /// Requires a bearer JWT issued by the Auth API with the <c>quotes:read</c> scope
    /// (<c>POST /api/v1/auth/login</c>); a valid token without the scope answers 403. Ids
    /// come from create responses and list items. Send <c>X-Correlation-Id</c> to
    /// correlate calls; it is echoed on every response and embedded in problem details.
    /// </remarks>
    /// <param name="id" example="3f2b8a9c1d4e5f6a7b8c9d0e1f2a3b4c">Quote identifier as returned by create or list.</param>
    /// <param name="http">Request context, not part of the HTTP contract.</param>
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <response code="200">The requested quote.</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:read</c> scope.</response>
    /// <response code="404">No quote matches the id (errorCode <c>quote.not_found</c>).</response>
    internal static async Task<IResult> GetByIdAsync(
        string id,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var service = http.RequestServices.GetRequiredService<QuoteGrpcService>();
        Quote quote;
        try
        {
            quote = await service.GetQuoteById(
                new GetQuoteByIdRequest { Id = id },
                new AdapterServerCallContext("/quotes.v2.QuoteService/GetQuoteById", cancellationToken));
        }
        catch (RpcException ex)
        {
            return GrpcErrorBridge.ToErrors(ex).ToProblem(http);
        }

        return new ProtoJsonResult(StatusCodes.Status200OK, null, ProtoJson.Format(quote));
    }

    /// <summary>Adds a quote to the catalog.</summary>
    /// <remarks>
    /// Requires a bearer JWT issued by the Auth API with the <c>quotes:write</c> scope
    /// (<c>POST /api/v1/auth/login</c>); read-only tokens answer 403. Catalog rules beyond
    /// the schema limits: text between 12 and 280 characters with at least 3 words, ending
    /// with '.', '!' or '?'; author between 2 and 80 characters (letters, spaces, hyphens,
    /// apostrophes and periods) and different from the text. Near-duplicates (same
    /// fingerprint, for example only punctuation changed) answer 409. The 201 response
    /// carries the created quote and a <c>Location</c> header addressing it. Send
    /// <c>X-Correlation-Id</c> to correlate calls; it is echoed on every response and
    /// embedded in problem details.
    /// </remarks>
    /// <param name="http">Request context, not part of the HTTP contract.</param>
    /// <param name="linkGenerator">Route address generator, not part of the HTTP contract.</param>
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <param name="request">The quote text and its author.</param>
    /// <response code="201">Quote created; the <c>Location</c> header addresses the new quote.</response>
    /// <response code="400">Catalog rule violation (errorCode <c>quote.text_too_short</c>, <c>quote.text_too_long</c>, <c>quote.text_needs_more_words</c>, <c>quote.text_must_end_with_punctuation</c>, <c>quote.author_too_short</c>, <c>quote.author_too_long</c>, <c>quote.author_invalid_characters</c> or <c>quote.author_equals_text</c>).</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:write</c> scope.</response>
    /// <response code="409">A quote with the same meaning already exists (errorCode <c>quote.duplicate_fingerprint</c>).</response>
    internal static async Task<IResult> CreateAsync(
        HttpRequest request,
        HttpContext http,
        LinkGenerator linkGenerator,
        CancellationToken cancellationToken)
    {
        string raw;
        using (var reader = new StreamReader(request.Body))
        {
            raw = await reader.ReadToEndAsync(cancellationToken);
        }

        CreateQuoteRequest protoRequest;
        try
        {
            protoRequest = ProtoJson.Parse<CreateQuoteRequest>(raw);
        }
        catch (Exception ex) when (ex is InvalidJsonException or InvalidProtocolBufferException)
        {
            // An unreadable body (empty, malformed, or a JSON-PB type mismatch) answers with
            // the same 400 problem the shared handler gives v0/v1 when their binding fails.
            return Results.Problem(ProblemDetailsBuilder.Build(
                StatusCodes.Status400BadRequest,
                ProblemDetailsBuilder.RequestValidationErrorCode,
                "The request body could not be read as JSON.",
                http));
        }

        if (ContractValidation.ValidateCreateQuote(protoRequest, http) is { } validationProblem)
        {
            return Results.Problem(validationProblem);
        }

        var service = http.RequestServices.GetRequiredService<QuoteGrpcService>();
        Quote quote;
        try
        {
            quote = await service.CreateQuote(
                protoRequest,
                new AdapterServerCallContext("/quotes.v2.QuoteService/CreateQuote", cancellationToken));
        }
        catch (RpcException ex)
        {
            return GrpcErrorBridge.ToErrors(ex).ToProblem(http);
        }

        var location = linkGenerator.GetUriByName(http, GetByIdRouteName, new { id = quote.Id });
        return new ProtoJsonResult(StatusCodes.Status201Created, location, ProtoJson.Format(quote));
    }
}
