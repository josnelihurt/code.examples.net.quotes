namespace Quotes.Domain.Tests;

public class QuoteAuthorTests
{
    [Fact]
    public void Create_normalizes_whitespace()
    {
        var result = QuoteAuthor.Create("  Leonardo da Vinci  ");

        result.IsError.ShouldBeFalse();
        result.Value.Value.ShouldBe("Leonardo da Vinci");
    }

    [Fact]
    public void Create_rejects_an_author_that_is_too_short()
    {
        var result = QuoteAuthor.Create("A");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_too_short");
    }

    [Fact]
    public void Create_rejects_an_author_that_is_too_long()
    {
        var result = QuoteAuthor.Create(new string('a', QuoteAuthor.MaxLength + 1));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_too_long");
    }

    [Fact]
    public void Create_rejects_author_with_digits()
    {
        var result = QuoteAuthor.Create("Author 42");

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("quote.author_invalid_characters");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromTrusted_rejects_a_blank_value(string value)
    {
        Should.Throw<ArgumentException>(() => QuoteAuthor.FromTrusted(value));
    }
}
