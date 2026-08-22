namespace Auth.Infrastructure.Tests;

public class HardcodedCredentialStoreTests
{
    private readonly HardcodedCredentialStore _sut = new();

    [Fact]
    public async Task ValidateAsync_accepts_the_local_credentials()
    {
        (await _sut.ValidateAsync("jrb", "supersecret", TestContext.Current.CancellationToken))
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData("jrb", "wrong")]
    [InlineData("someone", "supersecret")]
    [InlineData("JRB", "supersecret")]
    [InlineData("jrb", "SuperSecret")]
    [InlineData("", "")]
    public async Task ValidateAsync_rejects_anything_else(string username, string password)
    {
        (await _sut.ValidateAsync(username, password, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }
}
