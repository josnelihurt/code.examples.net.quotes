namespace Quotes.Domain;

public enum QuoteCreateError
{
    TextTooShort,
    TextTooLong,
    TextNeedsMoreWords,
    TextMustEndWithPunctuation,
    AuthorTooShort,
    AuthorTooLong,
    AuthorInvalidCharacters,
    AuthorEqualsText
}
