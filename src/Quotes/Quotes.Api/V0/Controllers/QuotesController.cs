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

    [HttpGet("random")]
    [Authorize(Policy = JwtAuthExtensions.ReadQuotesPolicy)]
    [ProducesResponseType<QuoteResponseDto>(StatusCodes.Status200OK, _jsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemContentType)]
    public async Task<ActionResult<QuoteResponseDto>> GetRandomAsync(CancellationToken cancellationToken)
    {
        var result = await _getRandomQuote.ExecuteAsync(cancellationToken);
        return result.Match<ActionResult<QuoteResponseDto>>(
            value => Ok(value.ToResponse()),
            errors => errors.ToActionResult(HttpContext));
    }

    [HttpGet("", Name = "ListQuotesV0")]
    [Authorize(Policy = JwtAuthExtensions.ReadQuotesPolicy)]
    [ProducesResponseType<QuotePageResponseDto>(StatusCodes.Status200OK, _jsonContentType)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, _problemContentType)]
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

    [HttpGet("{id}", Name = GetByIdRouteName)]
    [Authorize(Policy = JwtAuthExtensions.ReadQuotesPolicy)]
    [ProducesResponseType<QuoteResponseDto>(StatusCodes.Status200OK, _jsonContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, _problemContentType)]
    public async Task<ActionResult<QuoteResponseDto>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _getQuoteById.ExecuteAsync(id, cancellationToken);
        return result.Match<ActionResult<QuoteResponseDto>>(
            value => Ok(value.ToResponse()),
            errors => errors.ToActionResult(HttpContext));
    }

    [HttpPost("")]
    [Authorize(Policy = JwtAuthExtensions.WriteQuotesPolicy)]
    [ProducesResponseType<QuoteResponseDto>(StatusCodes.Status201Created, _jsonContentType)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, _problemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, _problemContentType)]
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
