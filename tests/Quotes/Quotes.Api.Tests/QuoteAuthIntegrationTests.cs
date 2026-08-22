using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Quotes.Api.V1.Contracts;
using Quotes.Api.V1.Endpoints;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests;

/// <summary>
/// Auth-focused integration tests over a slim pipeline (endpoint + JWT middleware only).
/// Full-pipeline coverage lives in <see cref="QuoteApiFullPipelineTests"/>.
/// </summary>
public class QuoteAuthIntegrationTests
{
    private const string _audience = "aspire-quotes-poc";
    private const string _issuer = "auth-api";

    private static readonly QuoteDto _sampleQuote = new("7", "Programs must be written for people to read.", "Harold Abelson");

    // Random per test run: tests never depend on a shared, committed key.
    private static readonly string _signingKey = $"test-key-{Guid.NewGuid():N}{Guid.NewGuid():N}";

    [Fact]
    public async Task Missing_bearer_token_returns_401_problem_with_www_authenticate()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ShouldNotBeEmpty();
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("title").GetString().ShouldBe("Unauthorized");
    }

    [Fact]
    public async Task Invalid_bearer_token_returns_401_with_invalid_token_challenge()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Single().Parameter.ShouldNotBeNull()
            .ShouldContain("error=\"invalid_token\"");
    }

    [Fact]
    public async Task Valid_bearer_token_returns_the_quote()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("jrb", "quotes:read", "quotes:write"));

        using var response = await client.GetAsync(
            new Uri("/api/v1/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponseDto>(TestContext.Current.CancellationToken);
        quote.ShouldNotBeNull();
        quote.Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task Create_without_bearer_token_returns_401()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto
            {
                Text = "Refactoring is the art of improving design.",
                Author = "Martin Fowler"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_with_a_token_lacking_the_write_scope_returns_403()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("jrb"));

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto
            {
                Text = "Refactoring is the art of improving design.",
                Author = "Martin Fowler"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_with_a_scoped_token_returns_201()
    {
        ErrorOr<QuoteDto> created = new QuoteDto("new-id", "Refactoring is the art of improving design.", "Martin Fowler");
        var create = Substitute.For<ICreateQuoteUseCase>();
        create.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(created);

        await using var app = await StartAsync(create);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("jrb", "quotes:read", "quotes:write"));

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/quotes", UriKind.Relative),
            new CreateQuoteRequestDto
            {
                Text = "Refactoring is the art of improving design.",
                Author = "Martin Fowler"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponseDto>(TestContext.Current.CancellationToken);
        quote.ShouldNotBeNull();
        quote.Id.ShouldBe("new-id");
    }

    private static async Task<WebApplication> StartAsync(ICreateQuoteUseCase? createUseCase = null)
    {
        var useCase = Substitute.For<IGetRandomQuoteUseCase>();
        ErrorOr<QuoteDto> sample = _sampleQuote;
        useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(sample);

        var getById = Substitute.For<IGetQuoteByIdUseCase>();
        ErrorOr<QuoteDto> notFound = Error.NotFound("quote.not_found", "Quote not found.");
        getById.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(notFound);

        createUseCase ??= Substitute.For<ICreateQuoteUseCase>();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Jwt:SigningKey"] = _signingKey;
        builder.Configuration["Jwt:Issuer"] = _issuer;
        builder.Configuration["Jwt:Audience"] = _audience;

        builder.AddStandardJwtAuthentication();
        builder.Services.AddSingleton(useCase);
        builder.Services.AddSingleton(getById);
        builder.Services.AddSingleton(createUseCase);
        builder.Services.AddValidation();

        var app = builder.Build();
        app.UseCorrelationId();
        app.UseStandardAuthentication();
        QuoteEndpoints.Map(app);
        await app.StartAsync();
        return app;
    }

    private static string CreateToken(string username, params string[] scopes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.Sub, username)
        };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
