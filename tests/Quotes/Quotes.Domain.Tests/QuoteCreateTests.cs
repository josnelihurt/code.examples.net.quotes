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
        result.Value.Text.Value.ShouldBe("Simplicity is the ultimate sophistication.");
        result.Value.Author.Value.ShouldBe("Leonardo da Vinci");
        result.Value.Id.ShouldNotBeNullOrWhiteSpace();
        result.Value.Fingerprint.Value.ShouldBe("simplicity is the ultimate sophistication");
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
    public void Create_propagates_text_validation_errors()
    {
        var result = Quote.Create("Too short.", "Ada Lovelace");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_too_short");
    }

    [Fact]
    public void Create_propagates_author_validation_errors()
    {
        var result = Quote.Create("Talk is cheap. Show me the code.", "A");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_too_short");
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
