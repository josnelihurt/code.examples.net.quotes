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
        builder.AddNpgsqlDbContext<QuotesDbContext>("quotesdb");
        builder.Services.AddScoped<IQuoteRepository, PostgresQuoteRepository>();
        return builder;
    }
}
