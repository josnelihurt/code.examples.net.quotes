using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quotes.Api.Tests;

/// <summary>
/// The seed serves the same catalog over two transports: v0 as MVC controllers, v1 as minimal
/// APIs. That claim is only worth making if the two are actually interchangeable, so every case
/// here drives both versions through the real host and compares what came back. A failure means
/// the versions have drifted, not that a single endpoint is broken.
/// </summary>
[Collection(WebHostCollection.Name)]
public class VersionParityTests(QuoteApiFactory factory) : IClassFixture<QuoteApiFactory>
{
    private readonly QuoteApiFactory _factory = factory;

    private HttpClient CreateClient(params string[] scopes)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.CreateToken(scopes.Length > 0 ? scopes : ["quotes:read", "quotes:write"]));
        return client;
    }

    private static Uri Route(string version, string suffix) =>
        new($"/api/{version}/quotes{suffix}", UriKind.Relative);

    /// <summary>Fields whose value changes per request but whose presence must still match.</summary>
    private static readonly string[] _volatileFields = ["correlationId", "traceId"];

    /// <summary>
    /// Reads the body as JSON with per-request values replaced by a placeholder rather than
    /// removed. Deleting them would also hide a version that omits the field altogether, which is
    /// exactly the kind of drift this suite exists to catch.
    /// </summary>
    private static async Task<JsonNode?> StableBodyAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var node = JsonNode.Parse(raw);
        if (node is JsonObject obj)
        {
            foreach (var field in _volatileFields)
            {
                if (obj.ContainsKey(field))
                {
                    obj[field] = "<per-request>";
                }
            }
        }

        return node;
    }

    private static void ShouldMatch(JsonNode? v0, JsonNode? v1) =>
        (v0?.ToJsonString() ?? "null").ShouldBe(v1?.ToJsonString() ?? "null");

    /// <summary>Asserts both versions answered with the same status, media type and body.</summary>
    private static async Task ShouldRespondIdenticallyAsync(HttpResponseMessage v0, HttpResponseMessage v1)
    {
        v0.StatusCode.ShouldBe(v1.StatusCode);
        v0.Content.Headers.ContentType?.MediaType.ShouldBe(v1.Content.Headers.ContentType?.MediaType);
        ShouldMatch(await StableBodyAsync(v0), await StableBodyAsync(v1));
    }

    [Theory]
    [InlineData("/random")]
    [InlineData("")]
    [InlineData("/7")]
    [InlineData("?page=1&pageSize=3")]
    public async Task A_read_endpoint_answers_identically_on_both_versions(string suffix)
    {
        using var client = CreateClient();

        using var v0 = await client.GetAsync(Route("v0", suffix), TestContext.Current.CancellationToken);
        using var v1 = await client.GetAsync(Route("v1", suffix), TestContext.Current.CancellationToken);

        // /random returns an arbitrary quote, so only status and media type are comparable.
        if (suffix == "/random")
        {
            v0.StatusCode.ShouldBe(v1.StatusCode);
            v0.Content.Headers.ContentType?.MediaType.ShouldBe(v1.Content.Headers.ContentType?.MediaType);
            return;
        }

        await ShouldRespondIdenticallyAsync(v0, v1);
    }

    [Fact]
    public async Task A_missing_quote_produces_the_same_404_problem_on_both_versions()
    {
        using var client = CreateClient();

        using var v0 = await client.GetAsync(Route("v0", "/does-not-exist"), TestContext.Current.CancellationToken);
        using var v1 = await client.GetAsync(Route("v1", "/does-not-exist"), TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ShouldRespondIdenticallyAsync(v0, v1);
    }

    [Fact]
    public async Task A_domain_validation_failure_produces_the_same_400_problem_on_both_versions()
    {
        using var client = CreateClient();
        // Passes the DTO's MaxLength guard but trips the domain's minimum-word rule.
        var body = new { text = "Short.", author = "Ada Lovelace" };

        using var v0 = await client.PostAsJsonAsync(Route("v0", ""), body, TestContext.Current.CancellationToken);
        using var v1 = await client.PostAsJsonAsync(Route("v1", ""), body, TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldRespondIdenticallyAsync(v0, v1);
    }

    [Fact]
    public async Task A_contract_validation_failure_produces_the_same_400_problem_on_both_versions()
    {
        using var client = CreateClient();
        // Empty text violates the DTO's [Required], so this never reaches the use case.
        var body = new { text = "", author = "" };

        using var v0 = await client.PostAsJsonAsync(Route("v0", ""), body, TestContext.Current.CancellationToken);
        using var v1 = await client.PostAsJsonAsync(Route("v1", ""), body, TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldRespondIdenticallyAsync(v0, v1);
    }

    [Fact]
    public async Task A_paging_validation_failure_produces_the_same_400_problem_on_both_versions()
    {
        using var client = CreateClient();

        using var v0 = await client.GetAsync(Route("v0", "?page=0"), TestContext.Current.CancellationToken);
        using var v1 = await client.GetAsync(Route("v1", "?page=0"), TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldRespondIdenticallyAsync(v0, v1);
    }

    [Fact]
    public async Task A_create_succeeds_on_both_versions_and_points_at_its_own_version()
    {
        using var client = CreateClient();

        using var v0 = await client.PostAsJsonAsync(
            Route("v0", ""),
            new { text = "Parity is proven by asserting it.", author = "Seed Author" },
            TestContext.Current.CancellationToken);
        using var v1 = await client.PostAsJsonAsync(
            Route("v1", ""),
            new { text = "Two transports, one catalog, one contract.", author = "Seed Author" },
            TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.Created);
        v1.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Each version must hand back a Location inside its own namespace.
        v0.Headers.Location!.ToString().ShouldContain("/api/v0/quotes/");
        v1.Headers.Location!.ToString().ShouldContain("/api/v1/quotes/");
    }

    [Fact]
    public async Task A_quote_created_on_one_version_is_readable_from_the_other()
    {
        using var client = CreateClient();

        using var created = await client.PostAsJsonAsync(
            Route("v0", ""),
            new { text = "One catalog behind both transports.", author = "Seed Author" },
            TestContext.Current.CancellationToken);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("id").GetString();

        using var readBack = await client.GetAsync(Route("v1", $"/{id}"), TestContext.Current.CancellationToken);

        readBack.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/random")]
    [InlineData("")]
    public async Task An_unauthenticated_read_is_rejected_the_same_way_on_both_versions(string suffix)
    {
        using var client = _factory.CreateClient();

        using var v0 = await client.GetAsync(Route("v0", suffix), TestContext.Current.CancellationToken);
        using var v1 = await client.GetAsync(Route("v1", suffix), TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        v1.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_without_the_write_scope_is_rejected_the_same_way_on_both_versions()
    {
        using var client = CreateClient("quotes:read");
        var body = new { text = "Scope enforcement is transport agnostic.", author = "Seed Author" };

        using var v0 = await client.PostAsJsonAsync(Route("v0", ""), body, TestContext.Current.CancellationToken);
        using var v1 = await client.PostAsJsonAsync(Route("v1", ""), body, TestContext.Current.CancellationToken);

        v0.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        v1.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
