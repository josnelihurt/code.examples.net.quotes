using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using ErrorOr;
using NSubstitute;

namespace Auth.Application.Tests;

public class AuthServiceTests
{
    private readonly ICredentialStore _credentials = Substitute.For<ICredentialStore>();
    private readonly AuthService _sut;
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();

    public AuthServiceTests()
    {
        _sut = new AuthService(_credentials, _tokens);
    }

    [Fact]
    public async Task LoginAsync_returns_a_result_when_credentials_are_accepted()
    {
        _credentials.Validate("jrb", "secret").Returns(true);
        _tokens.CreateToken("jrb", out int _).Returns(call =>
        {
            call[1] = 900;
            return "issued-token";
        });

        var result = await _sut.LoginAsync(
            new LoginRequest("jrb", "secret"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        result.Value.AccessToken.ShouldBe("issued-token");
        result.Value.Username.ShouldBe("jrb");
        result.Value.ExpiresIn.ShouldBe(900);
    }

    [Fact]
    public async Task LoginAsync_returns_invalid_credentials_when_the_store_rejects()
    {
        _credentials.Validate("jrb", "wrong").Returns(false);

        var result = await _sut.LoginAsync(
            new LoginRequest("jrb", "wrong"),
            TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("auth.invalid_credentials");
        result.FirstError.Type.ShouldBe(ErrorType.Unauthorized);
        _tokens.DidNotReceiveWithAnyArgs().CreateToken(default!, out int _);
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
        _credentials.DidNotReceiveWithAnyArgs().Validate(default!, default!);
    }

    [Fact]
    public void Validate_delegates_to_the_token_service()
    {
        _tokens.ValidateToken("token").Returns(new ValidateResult(true, "jrb"));

        var result = _sut.Validate("token");

        result.Valid.ShouldBeTrue();
        result.Username.ShouldBe("jrb");
    }

    [Fact]
    public void Validate_propagates_a_negative_result()
    {
        _tokens.ValidateToken("bad").Returns(new ValidateResult(false, null));

        var result = _sut.Validate("bad");

        result.Valid.ShouldBeFalse();
        result.Username.ShouldBeNull();
    }
}
