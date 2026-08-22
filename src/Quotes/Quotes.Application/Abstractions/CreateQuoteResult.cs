using Quotes.Domain;

namespace Quotes.Application.Abstractions;

public enum CreateQuoteStatus
{
    Created,
    Invalid,
    Conflict
}

public sealed record CreateQuoteResult(
    CreateQuoteStatus Status,
    QuoteDto? Quote = null,
    QuoteCreateError? Error = null);
