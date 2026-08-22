namespace Quotes.Application.Abstractions;

/// <summary>1-based page request. Validation happens in the use case; defaults live in <see cref="QuoteRules"/>.</summary>
public sealed record ListQuotesQuery(int Page, int PageSize);
