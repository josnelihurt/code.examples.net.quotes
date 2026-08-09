namespace Auth.Infrastructure.Tests;

public class HardcodedCredentialStoreTests
{
    private readonly HardcodedCredentialStore _sut = new();

    [Fact]
    public void Validate_accepts_the_poc_credentials()
    {
        _sut.Validate("jrb", "supersecret").ShouldBeTrue();
    }

    [Theory]
    [InlineData("jrb", "wrong")]
    [InlineData("someone", "supersecret")]
    [InlineData("JRB", "supersecret")]
    [InlineData("jrb", "SuperSecret")]
    [InlineData("", "")]
    public void Validate_rejects_anything_else(string username, string password)
    {
        _sut.Validate(username, password).ShouldBeFalse();
    }
}
