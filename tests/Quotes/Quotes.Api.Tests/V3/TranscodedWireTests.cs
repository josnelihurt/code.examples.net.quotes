using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests.V3;

/// <summary>
/// v3 demonstrates the stock platform runtime: ASP.NET Core's gRPC-JSON transcoding serves
/// the annotated proto directly, with no adapter in between. That platform makes different
/// default choices than the hand-written transports — a gRPC status envelope instead of
/// problem+json, 200 without Location on create, and no OpenAPI document because ApiExplorer
/// cannot see transcoded routes. This suite pins what is equivalent (auth middleware, success
/// bodies, the camelCase wire shape) and documents each deliberate drift as such: a change
/// here is either the platform moving or the v3 contract moving, never an accident in v2's
/// adapter.
/// </summary>
[Collection(WebHostCollection.Name)]
public class TranscodedWireTests(QuoteApiFactory factory) : IClassFixture<QuoteApiFactory>
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

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        raw.ShouldNotBeNullOrWhiteSpace();
        return JsonNode.Parse(raw)!;
    }

    /// <summary>The gRPC status envelope transcoding writes for every service failure.</summary>
    private static async Task AssertErrorEnvelopeAsync(HttpResponseMessage response, int code, string message)
    {
        response.StatusCode.ShouldBe((HttpStatusCode)GrpcCodeToHttp(code));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var error = await ReadJsonAsync(response);
        error["code"]!.GetValue<int>().ShouldBe(code);
        error["message"]!.GetValue<string>().ShouldBe(message);
        error["details"]!.AsArray().ShouldBeEmpty();
    }

    private static int GrpcCodeToHttp(int code) => code switch
    {
        3 => 400, // InvalidArgument
        5 => 404, // NotFound
        _ => 500
    };

    [Fact]
    public async Task Random_answers_the_same_camel_case_quote_body()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v3/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var quote = await ReadJsonAsync(response);
        quote["id"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        quote["text"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        quote["author"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task List_answers_the_same_page_shape_with_paging_scalars_on_page_one()
    {
        // The proto declares the paging scalars `optional` precisely so transcoding's JSON
        // writer emits them at proto defaults; a first page without "page":1 is the exact
        // regression that declaration exists to prevent.
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v3/quotes?page=1&pageSize=3", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadJsonAsync(response);
        page["items"]!.AsArray().Count.ShouldBe(3);
        foreach (var item in page["items"]!.AsArray())
        {
            item!["id"].ShouldNotBeNull();
        }

        page["page"]!.GetValue<int>().ShouldBe(1);
        page["pageSize"]!.GetValue<int>().ShouldBe(3);
        page["totalItems"]!.GetValue<int>().ShouldBeGreaterThanOrEqualTo(8);
        page["totalPages"]!.GetValue<int>()
            .ShouldBe((int)Math.Ceiling(page["totalItems"]!.GetValue<int>() / 3.0));
    }

    [Fact]
    public async Task Get_by_id_answers_the_same_quote()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v3/quotes/7", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await ReadJsonAsync(response);
        quote["id"]!.GetValue<string>().ShouldBe("7");
        quote["author"]!.GetValue<string>().ShouldBe("Harold Abelson");
    }

    [Fact]
    public async Task A_missing_quote_answers_the_grpc_status_envelope_not_problem_json()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v3/quotes/does-not-exist", UriKind.Relative),
            TestContext.Current.CancellationToken);

        await AssertErrorEnvelopeAsync(response, 5, "Quote not found.");
    }

    [Fact]
    public async Task Create_answers_200_with_the_quote_and_no_location_header()
    {
        // Drift from v0/v1/v2, pinned as deliberate: transcoding has no way to express
        // 201 + Location, so the created quote is the whole answer.
        using var client = CreateClient();
        var text = $"Transcoded creates answer 200 without Location {Guid.NewGuid():N}.";

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v3/quotes", UriKind.Relative),
            new { text, author = "Transcoding Suite" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
        var quote = await ReadJsonAsync(response);
        quote["id"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        quote["text"]!.GetValue<string>().ShouldBe(text);
    }

    [Fact]
    public async Task A_domain_validation_failure_answers_code_3_with_the_domain_message()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v3/quotes", UriKind.Relative),
            new { text = "Short.", author = "Ada Lovelace" },
            TestContext.Current.CancellationToken);

        await AssertErrorEnvelopeAsync(
            response,
            3,
            $"Quote text must be at least {QuoteRules.MinTextLength} characters.");
    }

    [Fact]
    public async Task Empty_fields_flow_to_domain_validation_instead_of_a_contract_layer()
    {
        // The v3 proto has no contract-level guards (v2 re-created them by hand in
        // ContractValidation), so an empty body reaches the domain rules and answers with
        // their message — v3's documented posture, not a bug to fix.
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v3/quotes", UriKind.Relative),
            new { text = "", author = "" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var error = await ReadJsonAsync(response);
        error["code"]!.GetValue<int>().ShouldBe(3);
        error["message"]!.GetValue<string>().ShouldContain("at least");
        error["details"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_malformed_body_answers_transcodings_own_json_message()
    {
        using var client = CreateClient();
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri("/api/v3/quotes", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        await AssertErrorEnvelopeAsync(response, 3, "Request JSON payload is not correctly formatted.");
    }

    [Fact]
    public async Task An_invalid_page_request_answers_code_3_with_the_shared_message()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v3/quotes?page=0", UriKind.Relative),
            TestContext.Current.CancellationToken);

        await AssertErrorEnvelopeAsync(
            response,
            3,
            "The requested page or page size is outside the allowed range.");
    }

    [Fact]
    public async Task An_unauthenticated_request_answers_the_shared_401_problem_exactly_like_v1()
    {
        // The auth middleware runs before the gRPC pipeline, so the 401 is byte-identical
        // to the other transports' — the one error path v3 never drifts on.
        using var client = _factory.CreateClient();

        using var v1 = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var v3 = await client.GetAsync(
            new Uri("/api/v3/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        v3.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        v3.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        v3.Headers.WwwAuthenticate.ShouldNotBeEmpty();

        v3.Content.Headers.ContentType!.MediaType
            .ShouldBe(v1.Content.Headers.ContentType!.MediaType);
        (await StableBodyAsync(v3)).ShouldBe(await StableBodyAsync(v1));
    }

    /// <summary>The 401 problem body with per-request fields replaced by a placeholder.</summary>
    private static async Task<string> StableBodyAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var node = JsonNode.Parse(raw)!;
        if (node is JsonObject obj)
        {
            foreach (var field in new[] { "correlationId", "traceId" })
            {
                if (obj.ContainsKey(field))
                {
                    obj[field] = "<per-request>";
                }
            }
        }

        return node.ToJsonString();
    }

    [Fact]
    public async Task A_read_only_token_gets_an_empty_403_like_every_transport()
    {
        using var client = CreateClient("quotes:read");

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v3/quotes", UriKind.Relative),
            new { text = "Transcoded writes need the write scope.", author = "Transcoding Suite" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_v3_openapi_document_is_served_from_the_proto_generated_artifact()
    {
        // ApiExplorer cannot see transcoded routes, so no runtime document exists. Instead,
        // the freeze pipeline generates one FROM the contract (comments, google.api.http
        // rules and openapiv2 options in the proto) and the host serves those exact bytes —
        // what Scalar shows is what the contract-drift job diffs. Swagger 2.0, because no
        // maintained generator emits OpenAPI 3 from google.api.http rules.
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/openapi/v3.json", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        document.GetProperty("swagger").GetString().ShouldBe("2.0");
        document.GetProperty("info").GetProperty("title").GetString().ShouldBe("Quotes.Api | v3");
        foreach (var path in (IEnumerable<string>)["/api/v3/quotes", "/api/v3/quotes/random", "/api/v3/quotes/{id}"])
        {
            document.GetProperty("paths").TryGetProperty(path, out _).ShouldBeTrue($"generated paths cover {path}");
        }

        // The summaries come from the proto's own leading comments.
        var list = document.GetProperty("paths").GetProperty("/api/v3/quotes").GetProperty("get");
        var summary = list.GetProperty("summary").GetString();
        summary.ShouldNotBeNull();
        summary.ShouldContain("Lists one page of the quote catalog");

        // The bearer requirement is declared in the contract's openapiv2 options.
        document.GetProperty("securityDefinitions").TryGetProperty("Bearer", out _).ShouldBeTrue();
        document.GetProperty("security").GetArrayLength().ShouldBeGreaterThan(0);
    }
}
