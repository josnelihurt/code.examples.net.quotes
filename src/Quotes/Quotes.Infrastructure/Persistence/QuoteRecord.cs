namespace Quotes.Infrastructure.Persistence;

public sealed class QuoteRecord
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string Author { get; init; }
    public required string NormalizedFingerprint { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
