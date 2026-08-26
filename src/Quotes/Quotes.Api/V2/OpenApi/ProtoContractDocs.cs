namespace Quotes.Api.V2.OpenApi;

/// <summary>
/// The OpenAPI documentation proto messages cannot carry themselves: JSON-PB field
/// descriptions, schema examples and the length limits v0/v1 declare as Data Annotations.
/// Values mirror the v1 contracts verbatim so <c>OpenApiParityTests</c> can hold the two
/// documents to each other (modulo schema names), and the mapping is total — the schema
/// transformer consults it for every message the v2 document exposes.
/// </summary>
internal static class ProtoContractDocs
{
    /// <summary>Schema example per message, keyed by proto full name.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Examples =
        new Dictionary<string, string>
        {
            ["quotes.v2.Quote"] =
                """{"id":"3f2b8a9c1d4e5f6a7b8c9d0e1f2a3b4c","text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}""",
            ["quotes.v2.CreateQuoteRequest"] =
                """{"text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}""",
            ["quotes.v2.ListQuotesResponse"] =
                """{"items":[{"id":"3f2b8a9c1d4e5f6a7b8c9d0e1f2a3b4c","text":"Talk is cheap. Show me the code.","author":"Linus Torvalds"}],"page":1,"pageSize":20,"totalItems":1,"totalPages":1}"""
        };

    /// <summary>Property description per <c>messageFullName.JsonName</c>.</summary>
    internal static readonly IReadOnlyDictionary<string, string> FieldDescriptions =
        new Dictionary<string, string>
        {
            ["quotes.v2.Quote.id"] = "Stable quote identifier.",
            ["quotes.v2.Quote.text"] = "Quote body text.",
            ["quotes.v2.Quote.author"] = "Attributed author of the quote.",
            ["quotes.v2.CreateQuoteRequest.text"] = "Quote body text.",
            ["quotes.v2.CreateQuoteRequest.author"] = "Attributed author of the quote.",
            ["quotes.v2.ListQuotesResponse.items"] = "Quotes on this page, in stable catalog order.",
            ["quotes.v2.ListQuotesResponse.page"] = "1-based page number this response represents.",
            ["quotes.v2.ListQuotesResponse.pageSize"] = "Number of items per page that was requested.",
            ["quotes.v2.ListQuotesResponse.totalItems"] = "Total number of quotes in the catalog.",
            ["quotes.v2.ListQuotesResponse.totalPages"] = "Total number of pages at the requested page size."
        };

    /// <summary>Maximum string length per <c>messageFullName.JsonName</c>; absent means unbounded.</summary>
    internal static readonly IReadOnlyDictionary<string, int> MaxLengths =
        new Dictionary<string, int>
        {
            ["quotes.v2.CreateQuoteRequest.text"] = Quotes.Application.Abstractions.QuoteRules.MaxTextLength,
            ["quotes.v2.CreateQuoteRequest.author"] = Quotes.Application.Abstractions.QuoteRules.MaxAuthorLength
        };
}
