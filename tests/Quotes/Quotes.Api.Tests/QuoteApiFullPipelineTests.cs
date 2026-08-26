using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AspireQuotesPoc.ServiceDefaults.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quotes.Api.V1.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests;

[Collection(WebHostCollection.Name)]
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
        problem.GetProperty("errorCode").GetString().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task V0_create_with_an_empty_body_field_returns_the_same_validation_envelope()
    {
        // The MVC transport must answer transport validation exactly like the minimal-API
        // transport: property-keyed errors plus the shared errorCode/correlationId envelope.
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v0/quotes", UriKind.Relative),
            new CreateQuoteRequestDto { Text = "", Author = "" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var errors = problem.GetProperty("errors");
        errors.GetProperty("Text").GetArrayLength().ShouldBeGreaterThan(0);
        errors.GetProperty("Author").GetArrayLength().ShouldBeGreaterThan(0);
        problem.GetProperty("errorCode").GetString().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
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
    public async Task List_returns_a_page_of_the_seeded_catalog()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes?page=1&pageSize=3", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<QuotePageResponseDto>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page.Items.Count.ShouldBe(3);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(3);
        // The fixture shares one catalog, so create tests may have appended quotes:
        // assert the arithmetic, not an exact total.
        page.TotalItems.ShouldBeGreaterThanOrEqualTo(8);
        page.TotalPages.ShouldBe((int)Math.Ceiling(page.TotalItems / 3.0));
    }

    [Fact]
    public async Task List_second_page_continues_without_overlapping_the_first()
    {
        using var client = CreateClient();

        using var firstResponse = await client.GetAsync(
            new Uri("/api/v1/quotes?page=1&pageSize=5", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var secondResponse = await client.GetAsync(
            new Uri("/api/v1/quotes?page=2&pageSize=5", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var first = await firstResponse.Content.ReadFromJsonAsync<QuotePageResponseDto>(TestContext.Current.CancellationToken);
        var second = await secondResponse.Content.ReadFromJsonAsync<QuotePageResponseDto>(TestContext.Current.CancellationToken);
        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        second.Page.ShouldBe(2);
        first.Items.Count.ShouldBe(5);
        // The fixture's catalog is shared with parallel creators, so appends can land
        // between the two fetches: the totals can only grow, and page two stays full
        // (five items) while the tail moves one page later.
        second.Items.Count.ShouldBe(Math.Min(second.TotalItems - 5, 5));
        second.TotalItems.ShouldBeGreaterThanOrEqualTo(first.TotalItems);
        first.Items.Select(quote => quote.Id)
            .Intersect(second.Items.Select(quote => quote.Id))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task List_without_query_parameters_uses_the_documented_defaults()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<QuotePageResponseDto>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(QuoteRules.DefaultPageSize);
        page.TotalItems.ShouldBeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task List_returns_a_400_problem_with_the_error_code_for_an_invalid_page()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes?page=0&pageSize=3", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("quote.invalid_page_request");
    }

    [Fact]
    public async Task List_returns_a_400_problem_when_the_page_size_exceeds_the_maximum()
    {
        // pageSize > 100 was only proven at application-unit level before; pin it at the
        // pipeline level so the transport mapping cannot regress silently.
        using var client = CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes?page=1&pageSize=101", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("quote.invalid_page_request");
    }

    [Fact]
    public async Task Concurrent_creates_of_the_same_quote_produce_exactly_one_winner()
    {
        // The flagship atomicity guarantee — unique fingerprint index + 23505 mapping —
        // proven under actual concurrency. A regression to check-then-insert passes every
        // sequential duplicate test but cannot pass this one.
        const int attempts = 6;
        var text = $"Concurrent publishers deserve exactly one winner {Guid.NewGuid():N}.";
        using var client = CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ =>
            client.PostAsJsonAsync(
                new Uri("/api/v1/quotes", UriKind.Relative),
                new CreateQuoteRequestDto { Text = text, Author = "Concurrency Suite" },
                TestContext.Current.CancellationToken)));

        var statuses = responses.Select(static r => r.StatusCode).OrderBy(static s => s).ToArray();
        statuses.Count(static s => s == HttpStatusCode.Created).ShouldBe(1);
        statuses.Count(static s => s == HttpStatusCode.Conflict).ShouldBe(attempts - 1);
    }

    [Theory]
    [InlineData("/api/v1/quotes")]
    [InlineData("/api/v0/quotes")]
    [InlineData("/api/v2/quotes")]
    public async Task Create_with_an_empty_body_returns_a_validation_problem(string path)
    {
        // A client that sends Content-Type: application/json and no payload is the
        // null-body adversarial case; the transports that share the problem+json
        // envelope must all answer it.
        using var client = CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri(path, UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("/api/v1/quotes")]
    [InlineData("/api/v0/quotes")]
    [InlineData("/api/v2/quotes")]
    public async Task Create_with_a_malformed_json_body_returns_a_validation_problem(string path)
    {
        using var client = CreateClient();
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri(path, UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("/api/v0/quotes/random")]
    [InlineData("/api/v1/quotes/random")]
    [InlineData("/api/v2/quotes/random")]
    public async Task An_unauthenticated_read_is_rejected_by_the_shared_middleware_on_every_transport(string path)
    {
        // Authorization runs before any transport code.
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri(path, UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ShouldNotBeEmpty();
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe(JwtAuthExtensions.TokenMissingErrorCode);
    }

    [Theory]
    [InlineData("/api/v0/quotes")]
    [InlineData("/api/v1/quotes")]
    [InlineData("/api/v2/quotes")]
    public async Task Create_without_the_write_scope_is_forbidden_on_every_transport(string path)
    {
        // Scope enforcement is transport-agnostic; the status is the comparable part.
        using var client = CreateClient(["quotes:read"]);

        using var response = await client.PostAsJsonAsync(
            new Uri(path, UriKind.Relative),
            new CreateQuoteRequestDto { Text = "Scope enforcement is transport agnostic.", Author = "Seed Author" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void The_public_development_signing_key_refuses_to_boot_quotes_api_in_production()
    {
        // The Production fail-fast guards were only unit-asserted before; this boots the
        // real composition root in the Production environment and proves the host cannot
        // come up on the public development key.
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Jwt:SigningKey", JwtAuthExtensions.DevelopmentSigningKey);
        });

        Should.Throw<InvalidOperationException>(() => factory.CreateClient())
            .Message.ShouldContain("development key");
    }

    [Fact]
    public async Task Health_degrades_while_the_catalog_database_is_paused()
    {
        // Readiness that cannot fail is worse than no probe: pause the real backing
        // container and prove /health leaves 200. The try/finally resumes and waits for
        // recovery so the rest of the suite never inherits a frozen database.
        using var client = CreateClient();

        using var baseline = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken);
        baseline.StatusCode.ShouldBe(HttpStatusCode.OK);

#pragma warning disable xUnit1051 // per-request timeouts are deliberate here: the paused database must not stall the whole test
        ContainerCli.Pause(QuoteApiFactory.ContainerId);
        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(40);
            while (true)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                using var response = await client.GetAsync(
                    new Uri("/health", UriKind.Relative),
                    cts.Token);
                if (response.StatusCode is not HttpStatusCode.OK)
                {
                    break;
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new Exception("/health stayed 200 for 40s while the backing PostgreSQL container was paused.");
                }

                await Task.Delay(500);
            }
        }
        finally
        {
            ContainerCli.Unpause(QuoteApiFactory.ContainerId);

            var recovered = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (true)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                using var response = await client.GetAsync(
                    new Uri("/health", UriKind.Relative),
                    cts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }

                if (DateTime.UtcNow > recovered)
                {
                    throw new Exception("/health did not recover within 30s after the container was resumed.");
                }

                await Task.Delay(500);
            }
        }
#pragma warning restore xUnit1051
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
        problem.GetProperty("errorCode").GetString().ShouldBe(JwtAuthExtensions.TokenMissingErrorCode);
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
