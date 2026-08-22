using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Quotes.Api.V1.Contracts;

namespace Quotes.Api.Tests;

public class QuoteApiFullPipelineTests : IClassFixture<QuoteApiFactory>
{
    private readonly QuoteApiFactory _factory;

    public QuoteApiFullPipelineTests(QuoteApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(bool includeScopes = true) =>
        CreateClient(includeScopes ? ["quotes:read", "quotes:write"] : []);

    private HttpClient CreateClient(string[] scopes)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.CreateToken(scopes));
        return client;
    }

    [Fact]
    public async Task GetRandom_returns_a_seeded_quote()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponseDto>(TestContext.Current.CancellationToken);
        quote.ShouldNotBeNull();
        quote.Text.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetById_resolves_a_seeded_quote()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/7", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponseDto>(TestContext.Current.CancellationToken);
        quote.ShouldNotBeNull();
        quote.Author.ShouldBe("Harold Abelson");
    }

    [Fact]
    public async Task GetById_returns_a_404_problem_for_an_unknown_id()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/nope", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("quote.not_found");
    }

    [Fact]
    public async Task GetById_returns_a_404_problem_for_a_whitespace_id()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/%20", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("quote.not_found");
    }

    [Fact]
    public async Task Create_returns_201_and_the_location_header_resolves()
    {
        using var client = CreateClient();
        var text = $"Integration quotes deserve unique bodies {Guid.NewGuid():N}.";

        using var created = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto { Text = text, Author = "Integration Test" },
            TestContext.Current.CancellationToken);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var location = created.Headers.Location.ShouldNotBeNull();
        location.AbsolutePath.ShouldStartWith("/api/v1/quotes/");

        using var fetched = await client.GetAsync(location, TestContext.Current.CancellationToken);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await fetched.Content.ReadFromJsonAsync<QuoteResponseDto>(TestContext.Current.CancellationToken);
        quote.ShouldNotBeNull();
        quote.Text.ShouldBe(text);
    }

    [Fact]
    public async Task Create_returns_a_409_problem_for_a_duplicate_fingerprint()
    {
        using var client = CreateClient();
        var text = $"Duplicates are rejected deterministically {Guid.NewGuid():N}.";

        using var first = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto { Text = text, Author = "Integration Test" },
            TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Same meaning, different punctuation and author: same fingerprint.
        using var second = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto
            {
                Text = text.TrimEnd('.') + "!",
                Author = "Somebody Else"
            },
            TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("quote.duplicate_fingerprint");
    }

    [Fact]
    public async Task Create_with_an_empty_body_field_returns_a_validation_problem()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto { Text = "", Author = "" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var errors = problem.GetProperty("errors");
        errors.GetProperty("Text").GetArrayLength().ShouldBeGreaterThan(0);
        errors.GetProperty("Author").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Create_with_domain_invalid_text_returns_a_problem_with_the_error_code()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto { Text = "Too short.", Author = "Ada Lovelace" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").GetProperty("quote.text_too_short").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Create_without_the_write_scope_returns_403()
    {
        using var client = CreateClient(includeScopes: false);

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto { Text = "Talk is cheap. Show me the code.", Author = "Linus Torvalds" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRandom_without_the_read_scope_returns_403()
    {
        using var client = CreateClient(["quotes:write"]);

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Requests_without_a_token_get_a_401_problem_with_a_correlation_id()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ShouldNotBeEmpty();
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_health_endpoint_answers_in_the_configured_environment()
    {
        // Probes must exist in every environment; the factory runs Development, and the
        // Production mapping is covered by ServiceDefaultsWiringTests.
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_real_composition_root_resolves_the_create_chain()
    {
        // Guard against a stubbed factory silently replacing the real pipeline.
        await using var scope = _factory.Services.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<Quotes.Application.Abstractions.ICreateQuoteUseCase>()
            .ShouldBeOfType<Quotes.Api.Telemetry.CreateQuoteUseCaseTelemetry>();
    }
}
