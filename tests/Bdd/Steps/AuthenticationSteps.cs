using System.Net;
using System.Text.Json;
using AspireQuotesPoc.Specs.Support;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Steps;

/// <summary>Vocabulary for exchanging credentials with the Auth API through the gateway.</summary>
[Binding]
public sealed class AuthenticationSteps(ApiWorld world)
{
    [Given("I am signed in as {string}")]
    public async Task GivenIAmSignedInAs(string username)
    {
        // Development credentials of the scaffolding store; scopes differ per account.
        var password = username switch
        {
            "jrb" => "supersecret",
            "reader" => "readsecret",
            _ => throw new ArgumentException($"No development credentials known for '{username}'.", nameof(username))
        };

        await SignInAsync(username, password);
        world.LastResponse.ShouldNotBeNull().StatusCode.ShouldBe(HttpStatusCode.OK);
        world.UseToken(world.AccessToken.ShouldNotBeNull());
    }

    [When("I sign in as {string} with password {string}")]
    public Task WhenISignInAsWithPassword(string username, string password) => SignInAsync(username, password);

    [When("I introspect the token {string}")]
    public async Task WhenIIntrospectTheToken(string token)
    {
        var response = await world.Client.PostAsync(
            "/api/v1/auth/validate",
            ApiWorld.JsonBody(JsonSerializer.Serialize(new { accessToken = token })));
        await world.RecordAsync(response);
    }

    [When("I introspect the current token")]
    public Task WhenIIntrospectTheCurrentToken() =>
        WhenIIntrospectTheToken(world.AccessToken.ShouldNotBeNull());

    [When("I introspect without a token")]
    public async Task WhenIIntrospectWithoutAToken()
    {
        var response = await world.Client.PostAsync("/api/v1/auth/validate", ApiWorld.JsonBody("{}"));
        await world.RecordAsync(response);
    }

    private async Task SignInAsync(string username, string password)
    {
        // A failed login must not leave a stale token behind for later steps.
        world.UseToken(null);
        world.AccessToken = null;

        var response = await world.Client.PostAsync(
            "/api/v1/auth/login",
            ApiWorld.JsonBody(JsonSerializer.Serialize(new { username, password })));
        await world.RecordAsync(response);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            world.AccessToken = world.LastBody?.GetProperty("accessToken").GetString();
            world.UseToken(world.AccessToken);
        }
    }
}
