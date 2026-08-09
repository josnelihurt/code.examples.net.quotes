using Auth.Domain;
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
    public void Login_returns_result_when_credentials_are_accepted()
    {
        _credentials.Validate("jrb", "secret").Returns(true);
        _tokens.CreateToken("jrb", out int _).Returns(call =>
        {
            call[1] = 900;
            return "issued-token";
        });

        var result = _sut.Login(new LoginRequest("jrb", "secret"));

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("issued-token");
        result.Username.ShouldBe("jrb");
        result.ExpiresIn.ShouldBe(900);
    }

    [Fact]
    public void Login_returns_null_when_credential_store_rejects()
    {
        _credentials.Validate("jrb", "wrong").Returns(false);

        _sut.Login(new LoginRequest("jrb", "wrong")).ShouldBeNull();
        _tokens.DidNotReceiveWithAnyArgs().CreateToken(default!, out int _);
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("jrb", "")]
    [InlineData("   ", "secret")]
    [InlineData("jrb", "   ")]
    [InlineData("", "")]
    public void Login_rejects_blank_input_without_touching_the_credential_store(string username, string password)
    {
        _sut.Login(new LoginRequest(username, password)).ShouldBeNull();

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
