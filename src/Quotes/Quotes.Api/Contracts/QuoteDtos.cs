namespace Quotes.Api.Contracts;

public sealed class QuoteResponseDto
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string Author { get; init; }
}

public sealed class ErrorResponseDto
{
    public required string Error { get; init; }
}
