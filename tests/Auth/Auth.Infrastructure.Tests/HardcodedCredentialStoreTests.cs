namespace Auth.Infrastructure.Tests;

public class HardcodedCredentialStoreTests
{
    private readonly HardcodedCredentialStore _sut = new();

    [Fact]
    public async Task ValidateAsync_grants_the_maintainer_both_scopes()
    {
        var decision = await _sut.ValidateAsync("jrb", "supersecret", TestContext.Current.CancellationToken);

        decision.IsValid.ShouldBeTrue();
        decision.Scopes.ShouldBe(["quotes:read", "quotes:write"], ignoreOrder: true);
    }

    [Fact]
    public async Task ValidateAsync_grants_the_reader_the_read_scope_only()
    {
        var decision = await _sut.ValidateAsync("reader", "readsecret", TestContext.Current.CancellationToken);

        decision.IsValid.ShouldBeTrue();
        decision.Scopes.ShouldBe(["quotes:read"]);
    }

    [Theory]
    [InlineData("jrb", "wrong")]
    [InlineData("reader", "supersecret")]
    [InlineData("someone", "supersecret")]
    [InlineData("JRB", "supersecret")]
    [InlineData("jrb", "SuperSecret")]
    [InlineData("", "")]
    public async Task ValidateAsync_rejects_anything_else(string username, string password)
    {
        var decision = await _sut.ValidateAsync(username, password, TestContext.Current.CancellationToken);

        decision.IsValid.ShouldBeFalse();
        decision.Scopes.ShouldBeEmpty();
    }
}
