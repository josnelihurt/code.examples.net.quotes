using Quotes.Domain;

namespace Quotes.Infrastructure.Persistence;

internal static class QuoteMapper
{
    public static Quote ToDomain(QuoteRecord record) =>
        Quote.Reconstitute(record.Id, record.Text, record.Author, record.NormalizedFingerprint);

    public static QuoteRecord ToRecord(Quote quote, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = quote.Id,
            Text = quote.Text,
            Author = quote.Author,
            NormalizedFingerprint = quote.NormalizedFingerprint,
            CreatedAtUtc = createdAtUtc
        };

    public static QuoteRecord Seed(string id, string text, string author, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = id,
            Text = text,
            Author = author,
            NormalizedFingerprint = Quote.ComputeFingerprint(text),
            CreatedAtUtc = createdAtUtc
        };
}
