using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Quotes.Api.Contracts;
using Quotes.Api.Endpoints;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests;

public class QuoteAuthIntegrationTests
{
    private const string _signingKey = "AspireQuotesPoc-Dev-Signing-Key-32chars!";
    private const string _issuer = "auth-api";
    private const string _audience = "aspire-quotes-poc";

    private static readonly QuoteDto _sampleQuote = new("7", "Programs must be written for people to read.", "Harold Abelson");

    [Fact]
    public async Task Missing_bearer_token_returns_401_with_error_body()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(
            new Uri("/api/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Error.ShouldBe("Unauthorized");
    }

    [Fact]
    public async Task Invalid_bearer_token_returns_401()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        using var response = await client.GetAsync(
            new Uri("/api/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_bearer_token_returns_the_quote()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("jrb"));

        using var response = await client.GetAsync(
            new Uri("/api/quotes/random", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<QuoteResponseDto>(TestContext.Current.CancellationToken);
        quote.ShouldNotBeNull();
        quote.Id.ShouldBe(_sampleQuote.Id);
        quote.Text.ShouldBe(_sampleQuote.Text);
        quote.Author.ShouldBe(_sampleQuote.Author);
    }

    [Fact]
    public async Task Create_without_bearer_token_returns_401()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/quotes/", UriKind.Relative),
            new CreateQuoteRequestDto
            {
                Text = "Refactoring is the art of improving design.",
                Author = "Martin Fowler"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_with_valid_bearer_token_returns_201()
    {
        var create = Substitute.For<ICreateQuoteUseCase>();
        create.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateQuoteResult(
                CreateQuoteStatus.Created,
                new QuoteDto("new-id", "Refactoring is the art of improving design.", "Martin Fowler")));

        await using var app = await StartAsync(create);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken("jrb"));

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/quotes/", UriKind.Relative),
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
        useCase.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(_sampleQuote);

        createUseCase ??= Substitute.For<ICreateQuoteUseCase>();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Jwt:SigningKey"] = _signingKey;
        builder.Configuration["Jwt:Issuer"] = _issuer;
        builder.Configuration["Jwt:Audience"] = _audience;

        builder.AddStandardJwtAuthentication();
        builder.Services.AddSingleton(useCase);
        builder.Services.AddSingleton(createUseCase);
        builder.Services.AddValidatorsFromAssemblyContaining<CreateQuoteRequestDtoValidator>();
        builder.Services.AddLogging();

        var app = builder.Build();
        app.UseStandardAuthentication();
        QuoteEndpoints.Map(app);
        await app.StartAsync();
        return app;
    }

    private static string CreateToken(string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: [new Claim(ClaimTypes.Name, username), new Claim(JwtRegisteredClaimNames.Sub, username)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ErrorBody
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
