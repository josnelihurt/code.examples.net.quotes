using AspireQuotesPoc.ServiceDefaults.Http;

namespace ServiceDefaults.Tests;

public class BearerTokenTests
{
    [Theory]
    [InlineData("Bearer abc123", "abc123")]
    [InlineData("bearer abc123", "abc123")]
    [InlineData("BEARER abc123", "abc123")]
    [InlineData("Bearer   abc123   ", "abc123")]
    [InlineData("Bearer a.b.c", "a.b.c")]
    public void A_well_formed_header_yields_the_token(string header, string expected)
    {
        BearerToken.TryParse(header, out var token).ShouldBeTrue();

        token.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc123")]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer")]
    [InlineData("Bearer ")]
    [InlineData("Bearer     ")]
    [InlineData("Bearer\tabc")]
    public void Anything_else_is_rejected_with_an_empty_token(string? header)
    {
        BearerToken.TryParse(header, out var token).ShouldBeFalse();

        token.ShouldBe(string.Empty);
    }
}
