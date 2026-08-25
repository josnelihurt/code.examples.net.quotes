using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Support;

/// <summary>
/// Boots the real AppHost once per test run and tears it down at the end. Unlike the
/// WebApplicationFactory suites, this exercises the actual orchestration: separate OS
/// processes for auth-api and quotes-api, the PostgreSQL + pgweb containers, YARP
/// routing through the gateway container, service discovery between them, and the
/// WithReference wiring that hands quotes-api its database connection. That also means
/// the Serilog frozen-logger constraint that forced WebHostCollection in the in-process
/// suites does not apply — each API owns its process, so nothing races on the static
/// Log.Logger.
/// </summary>
[Binding]
public static class AspireStack
{
    private static DistributedApplication? _app;

    /// <summary>Stamped before boot; only containers created after this belong to this run.</summary>
    public static DateTimeOffset SuiteStartUtc { get; private set; }

    /// <summary>Throws when the stack is not running, so a hook failure surfaces as a scenario failure.</summary>
    public static DistributedApplication Application =>
        _app ?? throw new InvalidOperationException("The distributed application is not running.");

    /// <summary>
    /// The gateway is the caller-facing entry point: it routes /api/v1/auth and both quote
    /// versions. The container also maps an https port, but the specs speak plain http to
    /// the http endpoint.
    /// </summary>
    public static HttpClient CreateGatewayClient() => Application.CreateHttpClient("gateway", "http");

    /// <summary>
    /// Direct client for one API service. The gateway only routes the /api prefixes, so
    /// service-level surfaces (OpenAPI documents, Scalar pages) are addressable here only.
    /// </summary>
    public static HttpClient CreateServiceClient(string resourceName) =>
        Application.CreateHttpClient(resourceName, "http");

    /// <summary>
    /// The runtime container name of this stack's PostgreSQL container. Identity comes
    /// from creation time: DCP publishes no host port for it (the connection string is
    /// served through DCP's own proxy), so name or port scans cannot distinguish this
    /// run's container from leftovers of crashed previous runs. Only containers created
    /// after the suite booted belong to this run.
    /// </summary>
    public static string GetPostgresContainerName() => ContainerRuntime.FindPostgresContainer(SuiteStartUtc);

    /// <summary>Diagnostic: every running container, for failure messages.</summary>
    public static string DescribeRunningContainers() => ContainerRuntime.DescribeRunningContainers();

    [BeforeTestRun]
    public static async Task StartAsync()
    {
        SuiteStartUtc = DateTimeOffset.UtcNow;
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AspireQuotesPoc_AppHost>();

        // The SPA and the docsify site are not part of any API scenario, and `docs` shells
        // out to `pnpm dlx docsify-cli`, which downloads on every start. Browser coverage
        // lives in frontend/e2e instead. IResourceCollection is IList<IResource> — no
        // RemoveAll, so materialise then remove.
        foreach (var resource in builder.Resources.Where(r => r.Name is "web" or "docs").ToList())
        {
            builder.Resources.Remove(resource);
        }

        // The auth endpoints are rate-limited 10 requests / 30 s per client IP; every
        // scenario that signs in spends from that same window. The whole suite logs in far
        // faster than 30 s, so the production-shaped limit would trip mid-run. Raise it for
        // the spec environment only — the 429 ProblemDetails shape itself is proven
        // in-process by AuthRateLimitTests.
        var authApi = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == "auth-api");
        builder.CreateResourceBuilder(authApi)
            .WithEnvironment("RateLimiting__Auth__PermitLimit", "100");

        // The AppHost declares jwt-signing-key as a parameter; in run mode the dashboard
        // would prompt for it. Any high-entropy value works — Auth signs with it at
        // startup and Quotes verifies with the same value, both inside this test run.
        builder.Configuration["Parameters:jwt-signing-key"] =
            $"bdd-signing-key-{Guid.NewGuid():N}{Guid.NewGuid():N}";

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        // Every wait is bounded: a hang would read as an infrastructure failure, and the
        // timeout turns it into a diagnosable error instead. The gateway container has no
        // /health endpoint (YARP answers whatever the cluster routes), so Running — not
        // Healthy — is the strongest state available for it.
        var ready = TimeSpan.FromMinutes(3);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("auth-api").WaitAsync(ready);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("quotes-api").WaitAsync(ready);
        await _app.ResourceNotifications
            .WaitForResourceAsync("gateway", KnownResourceStates.Running)
            .WaitAsync(ready);

        // Running only means the container process started; YARP binds its sockets a beat
        // later, and requests sent in between are reset. The gateway answers 404 on any
        // unrouted path, so "any HTTP response at all" is the readiness signal.
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        while (true)
        {
            try
            {
                using var _ = await CreateGatewayClient().GetAsync("/");
                break;
            }
            catch (HttpRequestException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(250);
            }
        }
    }

    [AfterTestRun]
    public static async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
