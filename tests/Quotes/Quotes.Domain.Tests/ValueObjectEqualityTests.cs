namespace Quotes.Domain.Tests;

/// <summary>
/// The glossary promises value objects compare by value; these tests keep the
/// hand-written equality members honest for all three.
/// </summary>
public class ValueObjectEqualityTests
{
    [Fact]
    public void Value_objects_with_the_same_value_are_equal()
    {
        QuoteText.FromTrusted("Programs must be written for people to read.")
            .ShouldBe(QuoteText.FromTrusted("Programs must be written for people to read."));
        QuoteAuthor.FromTrusted("Harold Abelson").ShouldBe(QuoteAuthor.FromTrusted("Harold Abelson"));
        QuoteFingerprint.FromTrusted("first solve then write")
            .ShouldBe(QuoteFingerprint.FromTrusted("first solve then write"));
    }

    [Fact]
    public void Equal_values_hash_equally()
    {
        var left = QuoteText.FromTrusted("Programs must be written for people to read.");
        var right = QuoteText.FromTrusted("Programs must be written for people to read.");

        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Value_objects_with_different_values_are_not_equal()
    {
        QuoteText.FromTrusted("Programs must be written for people to read.")
            .ShouldNotBe(QuoteText.FromTrusted("Everything should be made as simple as possible."));
        QuoteAuthor.FromTrusted("Harold Abelson").ShouldNotBe(QuoteAuthor.FromTrusted("Albert Einstein"));
        QuoteFingerprint.FromTrusted("first solve").ShouldNotBe(QuoteFingerprint.FromTrusted("then write"));
    }

    [Fact]
    public void Value_objects_are_not_equal_to_null_or_other_types()
    {
        var text = QuoteText.FromTrusted("Programs must be written for people to read.");

        text.Equals(null).ShouldBeFalse();
        text.Equals("Programs must be written for people to read.").ShouldBeFalse();
        (text == null).ShouldBeFalse();
        (null == text).ShouldBeFalse();
    }
}
