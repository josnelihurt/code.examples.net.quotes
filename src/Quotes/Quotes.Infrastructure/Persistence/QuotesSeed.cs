using Quotes.Infrastructure.Mapping;

namespace Quotes.Infrastructure.Persistence;

/// <summary>
/// The catalog every boot path ships: the same eight quotes (same ids, fixed timestamps)
/// the BDD and Playwright suites assert on. Baked into the InitialCreate migration via
/// <c>HasData</c>, so migrating an empty database always reproduces it.
/// </summary>
internal static class QuotesSeed
{
    private static readonly DateTimeOffset _seedCreatedAt =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static readonly QuoteRecord[] Records =
    [
        Seed("1", "Simplicity is the ultimate sophistication.", "Leonardo da Vinci"),
        Seed("2", "Code is like humor. When you have to explain it, it's bad.", "Cory House"),
        Seed("3", "First, solve the problem. Then, write the code.", "John Johnson"),
        Seed("4", "Experience is the name everyone gives to their mistakes.", "Oscar Wilde"),
        Seed("5", "The only way to go fast is to go well.", "Robert C. Martin"),
        Seed("6", "Make it work, make it right, make it fast.", "Kent Beck"),
        Seed("7", "Programs must be written for people to read.", "Harold Abelson"),
        Seed("8", "Talk is cheap. Show me the code.", "Linus Torvalds")
    ];

    private static QuoteRecord Seed(string id, string text, string author) =>
        QuoteMappingExtensions.Seed(id, text, author, _seedCreatedAt);
}
