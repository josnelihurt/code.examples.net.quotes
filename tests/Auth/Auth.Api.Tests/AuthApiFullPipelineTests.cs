using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Auth.Api.Contracts;
using Auth.Application.Abstractions;
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
    public async Task Issued_scope_claims_match_the_policies_the_resource_api_registers()
    {
        // Drift test: ServiceDefaults owns the policies, Auth.Application owns the minted
        // vocabulary; neither can reference the other, so this test pins them together.
        JwtAuthExtensions.ReadQuotesPolicy.ShouldBe(AuthorizationScopes.QuotesRead);
        JwtAuthExtensions.WriteQuotesPolicy.ShouldBe(AuthorizationScopes.QuotesWrite);
        JwtAuthExtensions.ScopeClaimType.ShouldBe(AuthorizationScopes.ClaimType);

        // The 401 challenge vocabulary lives in ServiceDefaults for the same reason.
        JwtAuthExtensions.TokenMissingErrorCode.ShouldBe(AuthErrors.MissingToken.Code);

        var token = await _factory.IssueTokenAsync();
        var scopes = ReadScopes(token);

        scopes.ShouldContain(JwtAuthExtensions.ReadQuotesScope);
        scopes.ShouldContain(JwtAuthExtensions.WriteQuotesScope);
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
