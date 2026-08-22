namespace Quotes.Domain.Tests;

public class QuoteFingerprintTests
{
    [Fact]
    public void Fingerprint_ignores_case_and_punctuation()
    {
        var left = QuoteText.ComputeFingerprint("Code is like humor!");
        var right = QuoteText.ComputeFingerprint("code is like humor.");

        left.ShouldBe(right);
        left.ShouldBe("code is like humor");
    }

    [Fact]
    public void FromText_matches_ComputeFingerprint_on_the_text_value()
    {
        var text = QuoteText.Create("Talk is cheap. Show me the code.").Value;
        var fingerprint = QuoteFingerprint.FromText(text);

        fingerprint.Value.ShouldBe(text.ComputeFingerprint());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromTrusted_rejects_a_blank_value(string value)
    {
        Should.Throw<ArgumentException>(() => QuoteFingerprint.FromTrusted(value));
    }
}
