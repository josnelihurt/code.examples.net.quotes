namespace Quotes.Domain.Tests;

public class QuoteCreateTests
{
    [Fact]
    public void Create_accepts_a_well_formed_quote()
    {
        var result = Quote.Create(
            "  Simplicity   is the ultimate sophistication.  ",
            "  Leonardo da Vinci  ");

        result.Succeeded.ShouldBeTrue();
        result.Quote.ShouldNotBeNull();
        result.Quote.Text.ShouldBe("Simplicity is the ultimate sophistication.");
        result.Quote.Author.ShouldBe("Leonardo da Vinci");
        result.Quote.Id.ShouldNotBeNullOrWhiteSpace();
        result.Quote.NormalizedFingerprint.ShouldBe("simplicity is the ultimate sophistication");
    }

    [Fact]
    public void Create_rejects_text_that_is_too_short()
    {
        var result = Quote.Create("Too short.", "Ada Lovelace");

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(QuoteCreateError.TextTooShort);
    }

    [Fact]
    public void Create_rejects_text_without_terminal_punctuation()
    {
        var result = Quote.Create(
            "Programs must be written for people to read",
            "Harold Abelson");

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(QuoteCreateError.TextMustEndWithPunctuation);
    }

    [Fact]
    public void Create_rejects_text_with_fewer_than_three_words()
    {
        var result = Quote.Create("Hello world!", "Ada Lovelace");

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(QuoteCreateError.TextNeedsMoreWords);
    }

    [Fact]
    public void Create_rejects_author_with_digits()
    {
        var result = Quote.Create(
            "Make it work, make it right, make it fast.",
            "Author 42");

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(QuoteCreateError.AuthorInvalidCharacters);
    }

    [Fact]
    public void Create_rejects_author_equal_to_text()
    {
        const string text = "Simple words make a point.";
        var result = Quote.Create(text, text);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(QuoteCreateError.AuthorEqualsText);
    }

    [Fact]
    public void Fingerprint_ignores_case_and_punctuation()
    {
        var left = Quote.ComputeFingerprint("Code is like humor!");
        var right = Quote.ComputeFingerprint("code is like humor.");

        left.ShouldBe(right);
        left.ShouldBe("code is like humor");
    }
}
