using AspireQuotesPoc.ServiceDefaults.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults.Tests;

public class CorrelationIdTests
{
    [Fact]
    public void GetCorrelationId_prefers_the_value_stashed_by_the_middleware()
    {
        var context = new DefaultHttpContext();
        context.Items[HttpHeaderNames.CorrelationId] = "from-items";
        context.Request.Headers[HttpHeaderNames.CorrelationId] = "from-header";

        context.GetCorrelationId().ShouldBe("from-items");
    }

    [Fact]
    public void GetCorrelationId_falls_back_to_the_request_header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HttpHeaderNames.CorrelationId] = "from-header";

        context.GetCorrelationId().ShouldBe("from-header");
    }

    [Fact]
    public void GetCorrelationId_invents_one_when_nothing_is_available()
    {
        var context = new DefaultHttpContext();

        var correlationId = context.GetCorrelationId();

        correlationId.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParseExact(correlationId, "N", out _).ShouldBeTrue();
    }

    [Fact]
    public void GetCorrelationId_ignores_a_non_string_item()
    {
        var context = new DefaultHttpContext();
        context.Items[HttpHeaderNames.CorrelationId] = 42;
        context.Request.Headers[HttpHeaderNames.CorrelationId] = "from-header";

        context.GetCorrelationId().ShouldBe("from-header");
    }

    [Fact]
    public async Task The_middleware_echoes_an_incoming_correlation_id()
    {
        await using var app = await StartPipelineAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(HttpHeaderNames.CorrelationId, "incoming-123");

        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.SendAsync(request, cancellationToken);

        response.Headers.GetValues(HttpHeaderNames.CorrelationId).ShouldContain("incoming-123");
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldBe("incoming-123");
    }

    [Fact]
    public async Task The_middleware_generates_a_correlation_id_when_none_is_supplied()
    {
        await using var app = await StartPipelineAsync();
        using var client = app.GetTestClient();

        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), cancellationToken);

        var generated = response.Headers.GetValues(HttpHeaderNames.CorrelationId).Single();
        Guid.TryParseExact(generated, "N", out _).ShouldBeTrue();
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldBe(generated);
    }

    [Fact]
    public async Task A_blank_incoming_correlation_id_is_replaced()
    {
        await using var app = await StartPipelineAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.CorrelationId, "   ");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var effective = response.Headers.GetValues(HttpHeaderNames.CorrelationId).Single();
        Guid.TryParseExact(effective, "N", out _).ShouldBeTrue();
    }

    private static async Task<WebApplication> StartPipelineAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();
        app.UseCorrelationId();
        app.MapGet("/", (HttpContext http) => http.GetCorrelationId());

        await app.StartAsync();
        return app;
    }
}
