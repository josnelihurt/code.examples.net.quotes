using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the persistence adapters only. Use cases are registered by
    /// <c>AddQuotesApplication</c>; the API host composes both.
    /// </summary>
    /// <remarks>
    /// The Aspire client integration resolves the connection from the
    /// <c>ConnectionStrings:quotesdb</c> key — the one the AppHost's <c>WithReference</c>
    /// injects — and layers health checks, OpenTelemetry tracing, and connection retries
    /// on top of the EF Core registration.
    /// </remarks>
    public static IHostApplicationBuilder AddQuotesInfrastructure(this IHostApplicationBuilder builder)
    {
        // The integration's default check opens a connection and can stall without a
        // deadline against a frozen database; the deliberate round-trip check below is
        // strictly better (bounded, proof of an actual answer), so the default is off.
        builder.AddNpgsqlDbContext<QuotesDbContext>("quotesdb", settings =>
            settings.DisableHealthChecks = true);
        builder.Services.AddScoped<IQuoteRepository, PostgresQuoteRepository>();
        builder.Services.AddHealthChecks()
            .AddCheck<QuotesDatabaseHealthCheck>("quotesdb-roundtrip");

        return builder;
    }
}
