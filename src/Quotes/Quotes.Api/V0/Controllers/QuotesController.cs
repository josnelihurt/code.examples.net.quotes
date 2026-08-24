using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quotes.Api.V0.Contracts;
using Quotes.Api.V0.Mapping;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V0.Controllers;

/// <summary>
/// The controller-based transport for the quote catalog. It is a peer of the minimal-API
/// implementation in <c>V1.Endpoints.QuoteEndpoints</c>, not a predecessor: both call the very
/// same use cases and answer with the same envelope, so the pair demonstrates that transport
/// style is a detail the layering keeps swappable.
/// </summary>
[ApiController]
[Route("api/v0/quotes")]
[Authorize]
[ApiExplorerSettings(GroupName = DocumentName)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, _problemContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, _problemContentType)]
[OpenApiProblemExample(
    StatusCodes.Status401Unauthorized,
    Title = "Unauthorized",
    Detail = "A valid bearer token is required.",
    ErrorCode = JwtAuthExtensions.TokenInvalidErrorCode)]
[OpenApiProblemExample(
    StatusCodes.Status403Forbidden,
    Title = "Forbidden",
    Detail = "The access token is missing the required scope (quotes:read or quotes:write).")]
public sealed class QuotesController(
    IGetRandomQuoteUseCase getRandomQuote,
    IGetQuoteByIdUseCase getQuoteById,
    IListQuotesUseCase listQuotes,
    ICreateQuoteUseCase createQuote) : ControllerBase
{
    /// <summary>OpenAPI document this version publishes into. See <c>AddStandardApiServices</c>.</summary>
    internal const string DocumentName = "v0";

    /// <summary>
    /// Version-local route name. Pointing at v1's name would make a v0 create respond with a
    /// Location header addressing the other version.
    /// </summary>
    internal const string GetByIdRouteName = "GetQuoteByIdV0";

    /// <summary>
    /// Problem responses carry the RFC 9457 media type; without this the generated document would
    /// advertise plain <c>application/json</c> and drift from v1's contract.
    /// </summary>
    private const string _problemContentType = "application/problem+json";

    /// <summary>Success payloads are plain JSON, declared per response so nothing leaks into the
    /// problem responses the way a class-level <c>[Produces]</c> would.</summary>
    private const string _jsonContentType = "application/json";

    private readonly IGetRandomQuoteUseCase _getRandomQuote = getRandomQuote;
    private readonly IGetQuoteByIdUseCase _getQuoteById = getQuoteById;
    private readonly IListQuotesUseCase _listQuotes = listQuotes;
    private readonly ICreateQuoteUseCase _createQuote = createQuote;

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
    [HttpGet("random")]
    [Authorize(Policy = JwtAuthExtensions.ReadQuotesPolicy)]
    [ProducesResponseType<QuoteResponseDto>(StatusCodes.Status200OK, _jsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemContentType)]
    [OpenApiProblemExample(
        StatusCodes.Status404NotFound,
        ErrorCode = "quote.not_found",
        Detail = "Quote not found.")]
    public async Task<ActionResult<QuoteResponseDto>> GetRandomAsync(CancellationToken cancellationToken)
    {
        var result = await _getRandomQuote.ExecuteAsync(cancellationToken);
        return result.Match<ActionResult<QuoteResponseDto>>(
            value => Ok(value.ToResponse()),
            errors => errors.ToActionResult(HttpContext));
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
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <response code="200">The requested page as <c>{ items, page, pageSize, totalItems, totalPages }</c>.</response>
    /// <response code="400">Page or pageSize outside the allowed range (errorCode <c>quote.invalid_page_request</c>).</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:read</c> scope.</response>
    [HttpGet("", Name = "ListQuotesV0")]
    [Authorize(Policy = JwtAuthExtensions.ReadQuotesPolicy)]
    [ProducesResponseType<QuotePageResponseDto>(StatusCodes.Status200OK, _jsonContentType)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, _problemContentType)]
    [OpenApiProblemExample(
        StatusCodes.Status400BadRequest,
        ErrorCode = "quote.invalid_page_request",
        Detail = "The requested page or page size is outside the allowed range.")]
    public async Task<ActionResult<QuotePageResponseDto>> ListAsync(
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QuoteRules.DefaultPageSize)
    {
        var result = await _listQuotes.ExecuteAsync(new ListQuotesQuery(page, pageSize), cancellationToken);
        return result.Match<ActionResult<QuotePageResponseDto>>(
            value => Ok(value.ToResponse()),
            errors => errors.ToActionResult(HttpContext));
    }

    /// <summary>Returns one quote by id.</summary>
    /// <remarks>
    /// Requires a bearer JWT issued by the Auth API with the <c>quotes:read</c> scope
    /// (<c>POST /api/v1/auth/login</c>); a valid token without the scope answers 403. Ids
    /// come from create responses and list items. Send <c>X-Correlation-Id</c> to
    /// correlate calls; it is echoed on every response and embedded in problem details.
    /// </remarks>
    /// <param name="id" example="3f2b8a9c1d4e5f6a7b8c9d0e1f2a3b4c">Quote identifier as returned by create or list.</param>
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <response code="200">The requested quote.</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:read</c> scope.</response>
    /// <response code="404">No quote matches the id (errorCode <c>quote.not_found</c>).</response>
    [HttpGet("{id}", Name = GetByIdRouteName)]
    [Authorize(Policy = JwtAuthExtensions.ReadQuotesPolicy)]
    [ProducesResponseType<QuoteResponseDto>(StatusCodes.Status200OK, _jsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemContentType)]
    [OpenApiProblemExample(
        StatusCodes.Status404NotFound,
        ErrorCode = "quote.not_found",
        Detail = "Quote not found.")]
    public async Task<ActionResult<QuoteResponseDto>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _getQuoteById.ExecuteAsync(id, cancellationToken);
        return result.Match<ActionResult<QuoteResponseDto>>(
            value => Ok(value.ToResponse()),
            errors => errors.ToActionResult(HttpContext));
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
    /// <param name="cancellationToken">Cooperative cancellation, not part of the HTTP contract.</param>
    /// <param name="body">The quote text and its author.</param>
    /// <response code="201">Quote created; the <c>Location</c> header addresses the new quote.</response>
    /// <response code="400">Catalog rule violation (errorCode <c>quote.text_too_short</c>, <c>quote.text_too_long</c>, <c>quote.text_needs_more_words</c>, <c>quote.text_must_end_with_punctuation</c>, <c>quote.author_too_short</c>, <c>quote.author_too_long</c>, <c>quote.author_invalid_characters</c> or <c>quote.author_equals_text</c>).</response>
    /// <response code="401">Missing or invalid bearer token (errorCode <c>auth.token_missing</c> or <c>auth.token_invalid</c>).</response>
    /// <response code="403">The token lacks the <c>quotes:write</c> scope.</response>
    /// <response code="409">A quote with the same meaning already exists (errorCode <c>quote.duplicate_fingerprint</c>).</response>
    [HttpPost("")]
    [Authorize(Policy = JwtAuthExtensions.WriteQuotesPolicy)]
    [ProducesResponseType<QuoteResponseDto>(StatusCodes.Status201Created, _jsonContentType)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, _problemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemContentType)]
    [OpenApiProblemExample(
        StatusCodes.Status400BadRequest,
        ErrorCode = "quote.text_too_short",
        Detail = "Quote text must be at least 12 characters.")]
    [OpenApiProblemExample(
        StatusCodes.Status409Conflict,
        ErrorCode = "quote.duplicate_fingerprint",
        Detail = "A quote with the same meaning already exists.")]
    public async Task<ActionResult<QuoteResponseDto>> CreateAsync(
        CreateQuoteRequestDto body,
        CancellationToken cancellationToken)
    {
        var result = await _createQuote.ExecuteAsync(body.ToCommand(), cancellationToken);
        return result.Match<ActionResult<QuoteResponseDto>>(
            value => CreatedAtRoute(GetByIdRouteName, new { id = value.Id }, value.ToResponse()),
            errors => errors.ToActionResult(HttpContext));
    }
}
