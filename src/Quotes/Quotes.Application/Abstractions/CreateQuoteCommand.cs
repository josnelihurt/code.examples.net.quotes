namespace Quotes.Application.Abstractions;

public sealed record CreateQuoteCommand(string Text, string Author);
