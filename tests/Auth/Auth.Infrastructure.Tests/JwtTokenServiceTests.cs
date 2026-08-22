using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Auth.Infrastructure.Tests;

public class JwtTokenServiceTests
{
    private const string _signingKey = "unit-test-signing-key-that-is-long-enough-1234567890";

    private static JwtTokenService CreateService(params (string Key, string Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = _signingKey,
            ["Jwt:Issuer"] = "auth-api",
            ["Jwt:Audience"] = "aspire-quotes-poc",
            ["Jwt:ExpiresInSeconds"] = "3600"
        };

        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new JwtTokenService(configuration, NullLogger<JwtTokenService>.Instance);
    }

    [Fact]
    public void Constructor_throws_when_the_signing_key_is_absent()
    {
        var configuration = new ConfigurationBuilder().Build();

        Should.Throw<InvalidOperationException>(
            () => new JwtTokenService(configuration, NullLogger<JwtTokenService>.Instance));
    }

    [Fact]
    public void CreateToken_reports_the_configured_lifetime()
    {
        var sut = CreateService(("Jwt:ExpiresInSeconds", "120"));

        var token = sut.CreateToken("jrb", out var expiresInSeconds);

        token.ShouldNotBeNullOrWhiteSpace();
        expiresInSeconds.ShouldBe(120);
    }

    [Fact]
    public void CreateToken_falls_back_to_an_hour_when_the_lifetime_is_not_a_number()
    {
        var sut = CreateService(("Jwt:ExpiresInSeconds", "not-a-number"));

        sut.CreateToken("jrb", out var expiresInSeconds);

        expiresInSeconds.ShouldBe(3600);
    }

    [Fact]
    public void A_freshly_issued_token_validates_and_carries_the_username()
    {
        var sut = CreateService();

        var token = sut.CreateToken("jrb", out _);
        var result = sut.ValidateToken(token);

        result.Valid.ShouldBeTrue();
        result.Username.ShouldBe("jrb");
    }

    [Fact]
    public void A_freshly_issued_token_carries_the_read_and_write_scopes()
    {
        var sut = CreateService();

        var token = sut.CreateToken("jrb", out _);
        var scopes = new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .Where(claim => claim.Type == "scope")
            .Select(claim => claim.Value)
            .ToList();

        scopes.ShouldContain("quotes:read");
        scopes.ShouldContain("quotes:write");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    public void ValidateToken_rejects_malformed_input(string token)
    {
        var result = CreateService().ValidateToken(token);

        result.Valid.ShouldBeFalse();
        result.Username.ShouldBeNull();
    }

    [Fact]
    public void ValidateToken_rejects_a_token_signed_with_a_different_key()
    {
        var issuer = CreateService(("Jwt:SigningKey", "a-completely-different-key-of-sufficient-length-99"));
        var verifier = CreateService();

        var result = verifier.ValidateToken(issuer.CreateToken("jrb", out _));

        result.Valid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateToken_rejects_a_token_from_another_issuer()
    {
        var issuer = CreateService(("Jwt:Issuer", "someone-else"));
        var verifier = CreateService();

        verifier.ValidateToken(issuer.CreateToken("jrb", out _)).Valid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateToken_rejects_a_token_for_another_audience()
    {
        var issuer = CreateService(("Jwt:Audience", "another-app"));
        var verifier = CreateService();

        verifier.ValidateToken(issuer.CreateToken("jrb", out _)).Valid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateToken_rejects_a_token_whose_payload_was_tampered_with()
    {
        var sut = CreateService();
        var token = sut.CreateToken("jrb", out _);
        var parts = token.Split('.');
        var tampered = string.Join('.', parts[0], parts[1], new string('a', parts[2].Length));

        sut.ValidateToken(tampered).Valid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateToken_rejects_an_expired_token()
    {
        // Clock skew inside JwtTokenService is one minute, so the token must be older than that.
        var sut = CreateService(("Jwt:ExpiresInSeconds", "-120"));

        sut.ValidateToken(sut.CreateToken("jrb", out _)).Valid.ShouldBeFalse();
    }
}
