using Quotes.Domain;

namespace Quotes.Infrastructure;

public sealed class InMemoryQuoteRepository : IQuoteRepository
{
    private static readonly Quote[] Quotes =
    [
        new() { Id = "1", Text = "Simplicity is the ultimate sophistication.", Author = "Leonardo da Vinci" },
        new() { Id = "2", Text = "Code is like humor. When you have to explain it, it's bad.", Author = "Cory House" },
        new() { Id = "3", Text = "First, solve the problem. Then, write the code.", Author = "John Johnson" },
        new() { Id = "4", Text = "Experience is the name everyone gives to their mistakes.", Author = "Oscar Wilde" },
        new() { Id = "5", Text = "The only way to go fast is to go well.", Author = "Robert C. Martin" },
        new() { Id = "6", Text = "Make it work, make it right, make it fast.", Author = "Kent Beck" },
        new() { Id = "7", Text = "Programs must be written for people to read.", Author = "Harold Abelson" },
        new() { Id = "8", Text = "Talk is cheap. Show me the code.", Author = "Linus Torvalds" }
    ];

    public Quote GetRandom() => Quotes[Random.Shared.Next(Quotes.Length)];
}
