namespace Quotes.Domain.Tests;

public class QuoteTextTests
{
    [Fact]
    public void Create_normalizes_whitespace()
    {
        var result = QuoteText.Create("  Simplicity   is the ultimate sophistication.  ");

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe("Simplicity is the ultimate sophistication.");
    }

    [Fact]
    public void Create_rejects_text_that_is_too_short()
    {
        var result = QuoteText.Create("Too short.");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_too_short");
    }

    [Fact]
    public void Create_rejects_text_that_is_too_long()
    {
        var result = QuoteText.Create(new string('a', QuoteText.MaxLength + 1) + ".");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_too_long");
    }

    [Fact]
    public void Create_rejects_text_without_terminal_punctuation()
    {
        var result = QuoteText.Create("Programs must be written for people to read");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_must_end_with_punctuation");
    }

    [Fact]
    public void Create_rejects_text_with_fewer_than_three_words()
    {
        var result = QuoteText.Create("Hello world!");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.text_needs_more_words");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromTrusted_rejects_a_blank_value(string value)
    {
        Should.Throw<ArgumentException>(() => QuoteText.FromTrusted(value));
    }
}
