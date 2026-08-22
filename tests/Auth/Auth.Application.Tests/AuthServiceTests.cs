using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using ErrorOr;
using NSubstitute;

namespace Auth.Application.Tests;

public class AuthServiceTests
{
    private readonly ICredentialStore _credentials = Substitute.For<ICredentialStore>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_credentials, _tokens);
    }

    [Fact]
    public async Task LoginAsync_returns_a_result_when_credentials_are_accepted()
    {
        var granted = new CredentialValidationResult(true, [AuthorizationScopes.QuotesRead]);
        _credentials.ValidateAsync("jrb", "secret", Arg.Any<CancellationToken>()).Returns(granted);
        _tokens.CreateTokenAsync("jrb", granted.Scopes, Arg.Any<CancellationToken>())
            .Returns(new IssuedToken("issued-token", 900));

        var result = await _sut.LoginAsync(
            new LoginRequest("jrb", "secret"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.AccessToken.ShouldBe("issued-token");
        result.Value.Username.ShouldBe("jrb");
        result.Value.ExpiresIn.ShouldBe(900);
    }

    [Fact]
    public async Task LoginAsync_forwards_the_scopes_the_store_granted_to_the_token_service()
    {
        var granted = new CredentialValidationResult(true, [AuthorizationScopes.QuotesRead, AuthorizationScopes.QuotesWrite]);
        _credentials.ValidateAsync("reader", "secret", Arg.Any<CancellationToken>()).Returns(granted);
        _tokens.CreateTokenAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedToken("issued-token", 900));

        await _sut.LoginAsync(
            new LoginRequest("reader", "secret"),
            TestContext.Current.CancellationToken);

        await _tokens.Received(1).CreateTokenAsync(
            "reader",
            Arg.Is<IReadOnlyList<string>>(scopes =>
                scopes != null
                && scopes.Contains(AuthorizationScopes.QuotesRead)
                && scopes.Contains(AuthorizationScopes.QuotesWrite)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_returns_invalid_credentials_when_the_store_rejects()
    {
        _credentials.ValidateAsync("jrb", "wrong", Arg.Any<CancellationToken>())
            .Returns(CredentialValidationResult.Invalid);

        var result = await _sut.LoginAsync(
            new LoginRequest("jrb", "wrong"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("auth.invalid_credentials");
        result.FirstError.Type.ShouldBe(ErrorType.Unauthorized);
        await _tokens.DidNotReceiveWithAnyArgs().CreateTokenAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("jrb", "")]
    [InlineData("   ", "secret")]
    [InlineData("jrb", "   ")]
    [InlineData("", "")]
    public async Task LoginAsync_rejects_blank_input_without_touching_the_credential_store(string username, string password)
    {
        var result = await _sut.LoginAsync(
            new LoginRequest(username, password),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("auth.invalid_credentials");
        await _credentials.DidNotReceiveWithAnyArgs().ValidateAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ValidateAsync_delegates_to_the_token_service()
    {
        _tokens.ValidateTokenAsync("token", Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(true, "jrb"));

        var result = await _sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeTrue();
        result.Username.ShouldBe("jrb");
    }

    [Fact]
    public async Task ValidateAsync_propagates_a_negative_result()
    {
        _tokens.ValidateTokenAsync("bad", Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(false, null));

        var result = await _sut.ValidateAsync("bad", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
        result.Username.ShouldBeNull();
    }
}
