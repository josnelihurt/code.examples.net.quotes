using System.ComponentModel;

namespace Quotes.Api.V1.Contracts;

/// <example>{"items":[{"id":"3f2b8a9c1d4e5f6a7b8c9d0e1f2a3b4c","text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}],"page":1,"pageSize":20,"totalItems":1,"totalPages":1}</example>
[Description("One page of the quote catalog.")]
public sealed class QuotePageResponseDto
{
    [Description("Quotes on this page, in stable catalog order.")]
    public required IReadOnlyList<QuoteResponseDto> Items { get; init; }

    [Description("1-based page number this response represents.")]
    public required int Page { get; init; }

    [Description("Number of items per page that was requested.")]
    public required int PageSize { get; init; }

    [Description("Total number of quotes in the catalog.")]
    public required int TotalItems { get; init; }

    [Description("Total number of pages at the requested page size.")]
    public required int TotalPages { get; init; }
}
