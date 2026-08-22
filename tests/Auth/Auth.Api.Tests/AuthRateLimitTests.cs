using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Auth.Api.Contracts;
using Auth.Api.Endpoints;
using Auth.Application.Abstractions;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Auth.Api.Tests;

/// <summary>
/// Slim pipeline (rate limiter + real auth endpoints + substituted auth service) with a
/// two-request window, so the limiter itself is under test. A second full
/// <c>WebApplicationFactory</c> in the assembly is not an option: Serilog's static
/// reloadable logger can only be frozen by one host per process.
/// </summary>
public class AuthRateLimitTests
{
    private static async Task<WebApplication> StartAsync()
    {
        var authService = Substitute.For<IAuthService>();
        ErrorOr<LoginResult> accepted = new LoginResult("issued-token", "jrb", 900);
        ErrorOr<LoginResult> rejected = AuthErrors.InvalidCredentials;
        authService
            .LoginAsync(Arg.Is<LoginRequest>(request => request != null && request.Password == "wrong"), Arg.Any<CancellationToken>())
            .Returns(rejected);
        authService
            .LoginAsync(Arg.Is<LoginRequest>(request => request != null && request.Password != "wrong"), Arg.Any<CancellationToken>())
            .Returns(accepted);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["RateLimiting:Auth:PermitLimit"] = "2";
        builder.Configuration["RateLimiting:Auth:WindowSeconds"] = "60";

        builder.Services.AddAuthRateLimiting(builder.Configuration);
        builder.Services.AddSingleton(authService);
        builder.Services.AddValidation();

        var app = builder.Build();
        app.UseCorrelationId();
        app.UseRateLimiter();
        AuthEndpoints.Map(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    [Fact]
    public async Task Requests_beyond_the_window_limit_get_a_429_problem_with_the_error_code()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var first = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "jrb", Password = "supersecret" },
            TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "jrb", Password = "wrong" },
            TestContext.Current.CancellationToken);
        second.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var third = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "jrb", Password = "supersecret" },
            TestContext.Current.CancellationToken);

        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await third.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCode").GetString().ShouldBe("auth.rate_limited");
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_rate_limit_partition_resets_for_a_new_host()
    {
        // Each host gets its own limiter state; this pins that the suite's per-test
        // StartAsync isolates windows instead of leaking across tests.
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequestDto { Username = "jrb", Password = "supersecret" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
