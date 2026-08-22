using ErrorOr;

namespace Quotes.Domain;

/// <summary>
/// Canonical quote errors. Codes are part of the public API contract: they surface as
/// ProblemDetails <c>errorCode</c> extensions, so renaming one is a breaking change.
/// </summary>
public static class QuoteErrors
{
    public static Error TextTooShort =>
        Error.Validation("quote.text_too_short", $"Quote text must be at least {QuoteText.MinLength} characters.");

    public static Error TextTooLong =>
        Error.Validation("quote.text_too_long", $"Quote text must be at most {QuoteText.MaxLength} characters.");

    public static Error TextNeedsMoreWords =>
        Error.Validation("quote.text_needs_more_words", $"Quote text must contain at least {QuoteText.MinWordCount} words.");

    public static Error TextMustEndWithPunctuation =>
        Error.Validation("quote.text_must_end_with_punctuation", "Quote text must end with '.', '!', or '?'.");

    public static Error AuthorTooShort =>
        Error.Validation("quote.author_too_short", $"Author must be at least {QuoteAuthor.MinLength} characters.");

    public static Error AuthorTooLong =>
        Error.Validation("quote.author_too_long", $"Author must be at most {QuoteAuthor.MaxLength} characters.");

    public static Error AuthorInvalidCharacters =>
        Error.Validation(
            "quote.author_invalid_characters",
            "Author may only contain letters (any alphabet), spaces, hyphens, apostrophes, and periods.");

    public static Error AuthorEqualsText =>
        Error.Validation("quote.author_equals_text", "Author must not be the same as the quote text.");

    public static Error NotFound =>
        Error.NotFound("quote.not_found", "Quote not found.");

    public static Error InvalidPageRequest =>
        Error.Validation("quote.invalid_page_request", "The requested page or page size is outside the allowed range.");

    public static Error DuplicateFingerprint =>
        Error.Conflict("quote.duplicate_fingerprint", "A quote with the same meaning already exists.");
}
