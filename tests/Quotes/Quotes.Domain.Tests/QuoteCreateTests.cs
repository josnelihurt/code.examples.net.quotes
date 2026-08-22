namespace Quotes.Domain.Tests;

public class QuoteCreateTests
{
    [Fact]
    public void Create_accepts_a_well_formed_quote()
    {
        var result = Quote.Create(
            "  Simplicity   is the ultimate sophistication.  ",
            "  Leonardo da Vinci  ");

        result.IsError.ShouldBeFalse();
        result.Value.Text.ShouldBe("Simplicity is the ultimate sophistication.");
        result.Value.Author.ShouldBe("Leonardo da Vinci");
        result.Value.Id.ShouldNotBeNullOrWhiteSpace();
        result.Value.NormalizedFingerprint.ShouldBe("simplicity is the ultimate sophistication");
    }

    [Fact]
    public void Create_rejects_text_that_is_too_short()
    {
        var result = Quote.Create("Too short.", "Ada Lovelace");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_too_short");
    }

    [Fact]
    public void Create_rejects_text_that_is_too_long()
    {
        var result = Quote.Create(
            new string('a', Quote.MaxTextLength + 1) + ".",
            "Ada Lovelace");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_too_long");
    }

    [Fact]
    public void Create_rejects_text_without_terminal_punctuation()
    {
        var result = Quote.Create(
            "Programs must be written for people to read",
            "Harold Abelson");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_must_end_with_punctuation");
    }

    [Fact]
    public void Create_rejects_text_with_fewer_than_three_words()
    {
        var result = Quote.Create("Hello world!", "Ada Lovelace");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_needs_more_words");
    }

    [Fact]
    public void Create_rejects_an_author_that_is_too_short()
    {
        var result = Quote.Create("Talk is cheap. Show me the code.", "A");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_too_short");
    }

    [Fact]
    public void Create_rejects_an_author_that_is_too_long()
    {
        var result = Quote.Create(
            "Talk is cheap. Show me the code.",
            new string('a', Quote.MaxAuthorLength + 1));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_too_long");
    }

    [Fact]
    public void Create_rejects_author_with_digits()
    {
        var result = Quote.Create(
            "Make it work, make it right, make it fast.",
            "Author 42");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_invalid_characters");
    }

    [Fact]
    public void Create_rejects_author_equal_to_text()
    {
        const string text = "Simple words make a point.";
        var result = Quote.Create(text, text);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_equals_text");
    }

    [Fact]
    public void Fingerprint_ignores_case_and_punctuation()
    {
        var left = Quote.ComputeFingerprint("Code is like humor!");
        var right = Quote.ComputeFingerprint("code is like humor.");

        left.ShouldBe(right);
        left.ShouldBe("code is like humor");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reconstitute_rejects_a_blank_id(string id)
    {
        Should.Throw<ArgumentException>(() => Quote.Reconstitute(
            id,
            "Programs must be written for people to read.",
            "Harold Abelson",
            "programs must be written for people to read"));
    }

    [Fact]
    public void Reconstitute_rejects_a_blank_fingerprint()
    {
        Should.Throw<ArgumentException>(() => Quote.Reconstitute(
            "7",
            "Programs must be written for people to read.",
            "Harold Abelson",
            "  "));
    }
}
