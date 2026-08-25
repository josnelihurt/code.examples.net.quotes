using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AspireQuotesPoc.ServiceDefaults.Errors;
using Auth.Api.Contracts;
using Auth.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Auth.Api.Tests;

public class AuthApiFullPipelineTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthApiFullPipelineTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_returns_a_token_and_echoes_the_correlation_id()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequestDto { Username = "jrb", Password = "supersecret" })
        };
        request.Headers.Add("X-Correlation-Id", "corr-full-1");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(TestContext.Current.CancellationToken);
        login.ShouldNotBeNull();
        login.AccessToken.ShouldNotBeNullOrWhiteSpace();
        login.Username.ShouldBe("jrb");
        login.CorrelationId.ShouldBe("corr-full-1");
    }

    [Fact]
    public async Task Login_with_wrong_credentials_returns_a_401_problem_with_the_error_code()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "jrb", Password = "wrong" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_with_an_empty_body_returns_a_400_validation_problem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "", Password = "" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var errors = problem.GetProperty("errors");
        errors.GetProperty("Username").GetArrayLength().ShouldBeGreaterThan(0);
        errors.GetProperty("Password").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Validate_answers_valid_for_an_issued_token()
    {
        using var client = _factory.CreateClient();
        var token = await _factory.IssueTokenAsync();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/validate", UriKind.Relative),
            new ValidateRequestDto { AccessToken = token },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Valid.ShouldBeTrue();
        body.Username.ShouldBe("jrb");
    }

    [Fact]
    public async Task Validate_accepts_the_token_from_the_authorization_header()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _factory.IssueTokenAsync());

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/validate", UriKind.Relative),
            new ValidateRequestDto(),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Valid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_answers_200_valid_false_for_a_garbage_token()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/validate", UriKind.Relative),
            new ValidateRequestDto { AccessToken = "not-a-jwt" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_answers_200_valid_false_for_a_foreign_signature()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/validate", UriKind.Relative),
            new ValidateRequestDto { AccessToken = _factory.IssueForeignToken() },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task Validate_without_any_token_returns_a_400_problem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/validate", UriKind.Relative),
            new ValidateRequestDto(),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("auth.token_missing");
    }

    [Fact]
    public async Task The_openapi_document_documents_both_operations()
    {
        using var client = _factory.CreateClient();

        var document = JsonNode.Parse(await client.GetStringAsync(
            "/openapi/v1.json", TestContext.Current.CancellationToken))!;

        document["info"]!["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();

        var login = document["paths"]!["/api/v1/auth/login"]!["post"]!;
        login["summary"].ShouldNotBeNull();
        login["description"].ShouldNotBeNull();
        // The XML-comment generator maps the LAST <param> tag to the request body, so the
        // body parameter must be documented last; this pins that the mapping stays correct.
        login["requestBody"]!["description"]!.GetValue<string>().ShouldContain("Credentials");
        login["responses"]!["401"]!["description"]!.GetValue<string>()
            .ShouldContain("auth.invalid_credentials");

        var validate = document["paths"]!["/api/v1/auth/validate"]!["post"]!;
        validate["summary"].ShouldNotBeNull();
        validate["responses"]!["400"]!["description"]!.GetValue<string>()
            .ShouldContain("auth.token_missing");

        var schemas = document["components"]!["schemas"]!.AsObject();
        schemas["LoginRequestDto"]!["example"].ShouldNotBeNull();
        schemas["LoginResponseDto"]!["example"].ShouldNotBeNull();

        var unauthorized = login["responses"]!["401"]!["content"]!["application/problem+json"]!;
        unauthorized["example"]!["errorCode"]!.GetValue<string>().ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task The_health_endpoint_answers()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_with_an_empty_body_returns_a_validation_problem()
    {
        // A client that sends Content-Type: application/json and no payload is the
        // null-body adversarial case.
        using var client = _factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_a_malformed_json_body_returns_a_validation_problem()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void The_scaffolding_credential_store_refuses_to_boot_in_production()
    {
        // The Production fail-fast was only unit-asserted before; this boots the real
        // composition root in the Production environment and proves the host cannot come
        // up on the scaffolding credential store.
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
        });

        Should.Throw<InvalidOperationException>(() => factory.CreateClient())
            .Message.ShouldContain("Production");
    }

    [Fact]
    public async Task Issued_tokens_carry_the_documented_scope_vocabulary()
    {
        // The Auth context can only pin its own side of the vocabulary: what the mint
        // puts into tokens. The resource side (QuoteScopes) cannot be referenced from
        // here — Architecture.Tests owns the cross-context drift pin.
        JwtAuthExtensions.ScopeClaimType.ShouldBe(AuthorizationScopes.ClaimType);

        var token = await _factory.IssueTokenAsync();
        var scopes = ReadScopes(token);

        scopes.ShouldContain(AuthorizationScopes.QuotesRead);
        scopes.ShouldContain(AuthorizationScopes.QuotesWrite);
    }

    [Fact]
    public async Task The_reader_login_mints_only_the_read_scope()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "reader", Password = "readsecret" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(TestContext.Current.CancellationToken);
        login.ShouldNotBeNull();

        var scopes = ReadScopes(login.AccessToken);
        scopes.ShouldBe([AuthorizationScopes.QuotesRead], ignoreOrder: true);
    }

    private static List<string> ReadScopes(string token) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .Where(claim => claim.Type == AuthorizationScopes.ClaimType)
            .Select(claim => claim.Value)
            .ToList();
}
