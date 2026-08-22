using System.IdentityModel.Tokens.Jwt;
using Auth.Application.Abstractions;
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
    public async Task CreateTokenAsync_reports_the_configured_lifetime()
    {
        var sut = CreateService(("Jwt:ExpiresInSeconds", "120"));

        var issued = await sut.CreateTokenAsync("jrb", TestContext.Current.CancellationToken);

        issued.AccessToken.ShouldNotBeNullOrWhiteSpace();
        issued.ExpiresInSeconds.ShouldBe(120);
    }

    [Fact]
    public async Task CreateTokenAsync_falls_back_to_an_hour_when_the_lifetime_is_not_a_number()
    {
        var sut = CreateService(("Jwt:ExpiresInSeconds", "not-a-number"));

        var issued = await sut.CreateTokenAsync("jrb", TestContext.Current.CancellationToken);

        issued.ExpiresInSeconds.ShouldBe(3600);
    }

    [Fact]
    public async Task A_freshly_issued_token_validates_and_carries_the_username()
    {
        var sut = CreateService();

        var issued = await sut.CreateTokenAsync("jrb", TestContext.Current.CancellationToken);
        var result = await sut.ValidateTokenAsync(issued.AccessToken, TestContext.Current.CancellationToken);

        result.Valid.ShouldBeTrue();
        result.Username.ShouldBe("jrb");
    }

    [Fact]
    public async Task A_freshly_issued_token_carries_the_read_and_write_scopes()
    {
        var sut = CreateService();

        var issued = await sut.CreateTokenAsync("jrb", TestContext.Current.CancellationToken);
        var scopes = new JwtSecurityTokenHandler()
            .ReadJwtToken(issued.AccessToken)
            .Claims
            .Where(claim => claim.Type == AuthorizationScopes.ClaimType)
            .Select(claim => claim.Value)
            .ToList();

        scopes.ShouldContain(AuthorizationScopes.QuotesRead);
        scopes.ShouldContain(AuthorizationScopes.QuotesWrite);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    public async Task ValidateTokenAsync_rejects_malformed_input(string token)
    {
        var result = await CreateService().ValidateTokenAsync(token, TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
        result.Username.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_rejects_a_token_signed_with_a_different_key()
    {
        var issuer = CreateService(("Jwt:SigningKey", "a-completely-different-key-of-sufficient-length-99"));
        var verifier = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        var issued = await issuer.CreateTokenAsync("jrb", cancellationToken);
        var result = await verifier.ValidateTokenAsync(issued.AccessToken, cancellationToken);

        result.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_rejects_a_token_from_another_issuer()
    {
        var issuer = CreateService(("Jwt:Issuer", "someone-else"));
        var verifier = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        var issued = await issuer.CreateTokenAsync("jrb", cancellationToken);

        (await verifier.ValidateTokenAsync(issued.AccessToken, cancellationToken)).Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_rejects_a_token_for_another_audience()
    {
        var issuer = CreateService(("Jwt:Audience", "another-app"));
        var verifier = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        var issued = await issuer.CreateTokenAsync("jrb", cancellationToken);

        (await verifier.ValidateTokenAsync(issued.AccessToken, cancellationToken)).Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_rejects_a_token_whose_payload_was_tampered_with()
    {
        var sut = CreateService();
        var cancellationToken = TestContext.Current.CancellationToken;

        var issued = await sut.CreateTokenAsync("jrb", cancellationToken);
        var parts = issued.AccessToken.Split('.');
        var tampered = string.Join('.', parts[0], parts[1], new string('a', parts[2].Length));

        (await sut.ValidateTokenAsync(tampered, cancellationToken)).Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_rejects_an_expired_token()
    {
        // Clock skew inside JwtTokenService is one minute, so the token must be older than that.
        var sut = CreateService(("Jwt:ExpiresInSeconds", "-120"));

        var issued = await sut.CreateTokenAsync("jrb", TestContext.Current.CancellationToken);

        (await sut.ValidateTokenAsync(issued.AccessToken, TestContext.Current.CancellationToken)).Valid.ShouldBeFalse();
    }
}
