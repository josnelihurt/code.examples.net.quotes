using Quotes.Domain;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure.Mapping;

internal static class QuoteMappingExtensions
{
    public static Quote ToDomain(this QuoteRecord record) =>
        Quote.Reconstitute(record.Id, record.Text, record.Author, record.NormalizedFingerprint);

    public static QuoteRecord ToRecord(this Quote quote, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = quote.Id,
            Text = quote.Text.Value,
            Author = quote.Author.Value,
            NormalizedFingerprint = quote.Fingerprint.Value,
            CreatedAtUtc = createdAtUtc
        };

    public static QuoteRecord Seed(string id, string text, string author, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = id,
            Text = text,
            Author = author,
            NormalizedFingerprint = QuoteText.ComputeFingerprint(text),
            CreatedAtUtc = createdAtUtc
        };
}
