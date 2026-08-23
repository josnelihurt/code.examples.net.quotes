using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the in-memory persistence adapters (the current default). Removed once the
    /// EF Core / PostgreSQL adapter takes over the composition root.
    /// </summary>
    public static IServiceCollection AddQuotesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IQuoteSelector, RandomQuoteSelector>();
        services.AddSingleton<IQuoteRepository, InMemoryQuoteRepository>();
        return services;
    }

    /// <summary>
    /// Registers the EF Core / PostgreSQL persistence adapters. The Aspire client
    /// integration resolves the connection from the <c>ConnectionStrings:quotesdb</c> key —
    /// the one the AppHost's <c>WithReference</c> injects — and layers health checks,
    /// OpenTelemetry tracing, and connection retries on top of the registration.
    /// </summary>
    public static IHostApplicationBuilder AddQuotesInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<QuotesDbContext>("quotesdb");
        builder.Services.AddScoped<IQuoteRepository, PostgresQuoteRepository>();
        return builder;
    }
}
