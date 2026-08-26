using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quotes.Api.Tests;

/// <summary>
/// The seed serves the same catalog over three interchangeable transports: v0 as MVC
/// controllers, v1 as minimal APIs, v2 as a proto contract served through a hand-written
/// adapter. That claim is only worth making if the transports really answer identically, so
/// every case here drives a pair of versions through the real host and compares what came
/// back — status, media type and the parsed JSON body with per-request fields stabilized.
/// A failure means the transports have drifted, not that a single endpoint is broken.
/// </summary>
/// <remarks>
/// v0 is pinned against v1, and v1 against v2: each pair must agree byte for byte (after
/// JSON parsing), which transitively holds v2 to the same wire shape v0 established.
/// </remarks>
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

    private static void ShouldMatch(JsonNode? first, JsonNode? second) =>
        (first?.ToJsonString() ?? "null").ShouldBe(second?.ToJsonString() ?? "null");

    /// <summary>Asserts both versions answered with the same status, media type and body.</summary>
    private static async Task ShouldRespondIdenticallyAsync(HttpResponseMessage first, HttpResponseMessage second)
    {
        first.StatusCode.ShouldBe(second.StatusCode);
        first.Content.Headers.ContentType?.MediaType.ShouldBe(second.Content.Headers.ContentType?.MediaType);
        ShouldMatch(await StableBodyAsync(first), await StableBodyAsync(second));
    }

    [Theory]
    [InlineData("v0", "v1", "/random")]
    [InlineData("v0", "v1", "")]
    [InlineData("v0", "v1", "/7")]
    [InlineData("v0", "v1", "?page=1&pageSize=3")]
    [InlineData("v1", "v2", "/random")]
    [InlineData("v1", "v2", "")]
    [InlineData("v1", "v2", "/7")]
    [InlineData("v1", "v2", "?page=1&pageSize=3")]
    public async Task A_read_endpoint_answers_identically_on_each_version_pair(
        string first, string second, string suffix)
    {
        using var client = CreateClient();

        using var left = await client.GetAsync(Route(first, suffix), TestContext.Current.CancellationToken);
        using var right = await client.GetAsync(Route(second, suffix), TestContext.Current.CancellationToken);

        // /random returns an arbitrary quote, so only status and media type are comparable.
        if (suffix == "/random")
        {
            left.StatusCode.ShouldBe(right.StatusCode);
            left.Content.Headers.ContentType?.MediaType.ShouldBe(right.Content.Headers.ContentType?.MediaType);
            return;
        }

        await ShouldRespondIdenticallyAsync(left, right);
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_missing_quote_produces_the_same_404_problem_on_each_version_pair(string first, string second)
    {
        using var client = CreateClient();

        using var left = await client.GetAsync(Route(first, "/does-not-exist"), TestContext.Current.CancellationToken);
        using var right = await client.GetAsync(Route(second, "/does-not-exist"), TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ShouldRespondIdenticallyAsync(left, right);
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_domain_validation_failure_produces_the_same_400_problem_on_each_version_pair(
        string first, string second)
    {
        using var client = CreateClient();
        // Passes the DTO's MaxLength guard but trips the domain's minimum-length rule.
        var body = new { text = "Short.", author = "Ada Lovelace" };

        using var left = await client.PostAsJsonAsync(Route(first, ""), body, TestContext.Current.CancellationToken);
        using var right = await client.PostAsJsonAsync(Route(second, ""), body, TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldRespondIdenticallyAsync(left, right);
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_contract_validation_failure_produces_the_same_400_problem_on_each_version_pair(
        string first, string second)
    {
        using var client = CreateClient();
        // Empty text violates the contract's required-field guard, so this never reaches
        // the use case.
        var body = new { text = "", author = "" };

        using var left = await client.PostAsJsonAsync(Route(first, ""), body, TestContext.Current.CancellationToken);
        using var right = await client.PostAsJsonAsync(Route(second, ""), body, TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldRespondIdenticallyAsync(left, right);
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_paging_validation_failure_produces_the_same_400_problem_on_each_version_pair(
        string first, string second)
    {
        using var client = CreateClient();

        using var left = await client.GetAsync(Route(first, "?page=0"), TestContext.Current.CancellationToken);
        using var right = await client.GetAsync(Route(second, "?page=0"), TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldRespondIdenticallyAsync(left, right);
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_create_succeeds_on_each_version_and_points_at_its_own_version(string first, string second)
    {
        using var client = CreateClient();

        using var left = await client.PostAsJsonAsync(
            Route(first, ""),
            new { text = $"Parity is proven by asserting it {Guid.NewGuid():N}.", author = "Seed Author" },
            TestContext.Current.CancellationToken);
        using var right = await client.PostAsJsonAsync(
            Route(second, ""),
            new { text = $"Two transports, one catalog, one contract {Guid.NewGuid():N}.", author = "Seed Author" },
            TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.Created);
        right.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Each version must hand back a Location inside its own namespace.
        left.Headers.Location!.ToString().ShouldContain($"/api/{first}/quotes/");
        right.Headers.Location!.ToString().ShouldContain($"/api/{second}/quotes/");
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_quote_created_on_one_version_is_readable_from_the_other(string first, string second)
    {
        using var client = CreateClient();

        using var created = await client.PostAsJsonAsync(
            Route(first, ""),
            new { text = $"One catalog behind every transport {Guid.NewGuid():N}.", author = "Seed Author" },
            TestContext.Current.CancellationToken);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("id").GetString();

        using var readBack = await client.GetAsync(Route(second, $"/{id}"), TestContext.Current.CancellationToken);

        readBack.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("v0", "v1", "/random")]
    [InlineData("v0", "v1", "")]
    [InlineData("v1", "v2", "/random")]
    [InlineData("v1", "v2", "")]
    public async Task An_unauthenticated_read_is_rejected_the_same_way_on_each_version_pair(
        string first, string second, string suffix)
    {
        using var client = _factory.CreateClient();

        using var left = await client.GetAsync(Route(first, suffix), TestContext.Current.CancellationToken);
        using var right = await client.GetAsync(Route(second, suffix), TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        right.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("v0", "v1")]
    [InlineData("v1", "v2")]
    public async Task A_token_without_the_write_scope_is_rejected_the_same_way_on_each_version_pair(
        string first, string second)
    {
        using var client = CreateClient("quotes:read");
        var body = new { text = "Scope enforcement is transport agnostic.", author = "Seed Author" };

        using var left = await client.PostAsJsonAsync(Route(first, ""), body, TestContext.Current.CancellationToken);
        using var right = await client.PostAsJsonAsync(Route(second, ""), body, TestContext.Current.CancellationToken);

        left.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        right.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
