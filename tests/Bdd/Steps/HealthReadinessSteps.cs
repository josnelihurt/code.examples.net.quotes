using System.Net;
using AspireQuotesPoc.Specs.Support;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Steps;

/// <summary>
/// The health-readiness journey: stop the real catalog database container and prove the
/// quotes API's /health leaves 200. A stop (not a pause) is deliberate: a frozen container
/// can keep pooled sockets looking alive through relays, while nothing survives the
/// database process going away. The AfterScenario hook always restarts the container and
/// waits for the endpoint to recover — a dead database must never leak into the scenarios
/// that follow.
/// </summary>
[Binding]
public sealed class HealthReadinessSteps
{
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan _degradationDeadline = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan _recoveryDeadline = TimeSpan.FromSeconds(60);

    private string? _stoppedContainer;

    [When("the catalog database container is stopped")]
    public void WhenTheCatalogDatabaseIsStopped()
    {
        // Identify this run's container by creation time: DCP publishes no host port for
        // it, so name or port scans cannot tell the stack's container from leftovers of
        // crashed previous runs on a contributor machine.
        _stoppedContainer = AspireStack.GetPostgresContainerName();
        ContainerRuntime.Stop(_stoppedContainer);
    }

    [Then("the quotes API health endpoint reports unhealthy")]
    public async Task ThenTheQuotesApiHealthReportsUnhealthy()
    {
        using var client = AspireStack.CreateServiceClient("quotes-api");
        // A probe against the stopped database can hang on the connection timeout far
        // longer than a healthy one, so each poll gets its own short deadline instead of
        // the HttpClient default (100s) that would swallow the whole assertion window.
        client.Timeout = _probeTimeout;

        var deadline = DateTime.UtcNow + _degradationDeadline;
        while (true)
        {
            try
            {
                using var response = await client.GetAsync("/health");
                if (response.StatusCode is not HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The API itself is still up; transient request failures while sockets to
                // the database drain are part of the degradation being observed.
            }
            catch (TaskCanceledException)
            {
                // A probe that hit the per-request timeout is the pause doing its job:
                // keep polling until the endpoint answers degraded or the window closes.
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new Exception(
                    $"/health answered 200 for the whole degradation window while the catalog database was stopped. " +
                    $"Stopped container: {_stoppedContainer}. Running containers: {AspireStack.DescribeRunningContainers()}");
            }

            await Task.Delay(500);
        }
    }

    [AfterScenario]
    public async Task ResumeTheCatalogDatabaseAsync()
    {
        if (_stoppedContainer is null)
        {
            return;
        }

        ContainerRuntime.Start(_stoppedContainer);
        _stoppedContainer = null;

        // Wait for the endpoint to answer 200 again so the next scenario starts against
        // a recovered catalog instead of inheriting draining connections.
        using var client = AspireStack.CreateServiceClient("quotes-api");
        client.Timeout = _probeTimeout;
        var deadline = DateTime.UtcNow + _recoveryDeadline;
        while (true)
        {
            try
            {
                using var response = await client.GetAsync("/health");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new Exception("/health did not recover within 60s after the catalog database was restarted.");
            }

            await Task.Delay(500);
        }
    }
}
