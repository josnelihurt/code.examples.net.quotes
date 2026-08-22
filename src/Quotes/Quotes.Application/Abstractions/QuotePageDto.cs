namespace Quotes.Application.Abstractions;

/// <summary>One page of quotes with the arithmetic a client needs to build page navigation.</summary>
public sealed record QuotePageDto(
    IReadOnlyList<QuoteDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
