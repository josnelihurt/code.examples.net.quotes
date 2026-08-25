using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Quotes.Infrastructure.Persistence;

/// <summary>
/// A health check that actually round-trips the catalog database. The Aspire Npgsql
/// integration's default check opens a connection — which a warm connection pool can
/// satisfy without touching the network, so a paused database still reports healthy.
/// This check executes <c>SELECT 1</c> under a hard five-second wall clock, so readiness
/// always answers (degraded) within seconds of the database becoming unreachable.
/// </summary>
internal sealed class QuotesDatabaseHealthCheck : IHealthCheck
{
    private static readonly TimeSpan _roundtripBudget = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;

    public QuotesDatabaseHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        try
        {
            var database = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
            var roundtrip = database.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

            // The wall clock is a Task.WhenAny guard rather than a token handed to the
            // query: a socket frozen mid-read can ignore cooperative cancellation, and
            // the check must complete regardless.
            var winner = await Task.WhenAny(roundtrip, Task.Delay(_roundtripBudget, cancellationToken));
            if (winner != roundtrip)
            {
                return HealthCheckResult.Unhealthy("Catalog database round-trip exceeded the five-second budget.");
            }

            await roundtrip;
            return HealthCheckResult.Healthy("Catalog database round-trip succeeded.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Catalog database round-trip failed.", ex);
        }
        finally
        {
            // Disposal is moved off the critical path deliberately: disposing the scope
            // disposes the DbContext, whose dispose waits for the in-flight query — and a
            // frozen socket would otherwise stall the very probe that is reporting the
            // outage. The abandoned work dies with the socket.
            var scopeCopy = scope;
            _ = Task.Run(async () =>
            {
                try
                {
                    await scopeCopy.DisposeAsync();
                }
                catch (Exception)
                {
                    // A scope behind a dead socket may throw on dispose; the probe has
                    // already reported, so the cleanup failure is irrelevant.
                }
            });
        }
    }
}
