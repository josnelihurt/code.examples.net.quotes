using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ServiceDiscovery;

namespace ServiceDefaults.Tests;

public class ServiceDefaultsWiringTests
{
    private static async Task<WebApplication> StartAsync(string environment, string? otlpEndpoint = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment
        });
        builder.WebHost.UseTestServer();

        if (otlpEndpoint is not null)
        {
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlpEndpoint;
        }

        builder.AddServiceDefaults();
        builder.AddStandardApiServices();

        var app = builder.Build();
        app.UseSerilogDefaults();
        app.UseCorrelationId();
        app.MapDefaultEndpoints();
        app.MapStandardApiDocumentation();

        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Development_exposes_the_health_and_liveness_endpoints()
    {
        await using var app = await StartAsync(Environments.Development);
        using var client = app.GetTestClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);
        using var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Production_maps_the_health_endpoints_for_orchestrator_probes()
    {
        await using var app = await StartAsync(Environments.Production);
        using var client = app.GetTestClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var health = await client.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken);
        using var alive = await client.GetAsync(new Uri("/alive", UriKind.Relative), cancellationToken);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        alive.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_openapi_document_is_served()
    {
        await using var app = await StartAsync(Environments.Development);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(
            new Uri("/openapi/v1.json", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_scalar_reference_page_is_served()
    {
        await using var app = await StartAsync(Environments.Development);
        using var client = app.GetTestClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Scalar redirects the bare path to the trailing-slash form.
        using var redirect = await client.GetAsync(new Uri("/scalar", UriKind.Relative), cancellationToken);
        redirect.StatusCode.ShouldBe(HttpStatusCode.Found);
        redirect.Headers.Location!.ToString().ShouldBe("scalar/");

        using var page = await client.GetAsync(new Uri("/scalar/", UriKind.Relative), cancellationToken);
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Service_discovery_is_registered_for_http_clients()
    {
        await using var app = await StartAsync(Environments.Development);

        app.Services.GetService<ServiceEndpointResolver>().ShouldNotBeNull();
    }

    [Fact]
    public async Task The_self_health_check_is_registered_and_tagged_live()
    {
        await using var app = await StartAsync(Environments.Development);

        var report = await app.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.ShouldContainKey("self");
        report.Entries["self"].Tags.ShouldContain("live");
    }

    [Fact]
    public async Task Configuring_an_otlp_endpoint_still_produces_a_working_host()
    {
        // Exercises the exporter and Serilog OTLP sink branches; nothing has to be listening.
        await using var app = await StartAsync(Environments.Development, "http://localhost:4317");
        using var client = app.GetTestClient();

        using var health = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken);

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
