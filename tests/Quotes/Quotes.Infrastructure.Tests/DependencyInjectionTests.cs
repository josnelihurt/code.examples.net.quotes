using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddQuotesInfrastructure_resolves_the_persistence_adapters()
    {
        var builder = Host.CreateApplicationBuilder();
        // The connection string only has to parse — nothing connects at registration time;
        // the AppHost (or a standalone run) injects the real value via WithReference.
        builder.Configuration["ConnectionStrings:quotesdb"] =
            "Host=localhost;Port=5432;Database=quotes;Username=postgres;Password=postgres";

        builder.AddQuotesInfrastructure();
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<QuotesDbContext>().ShouldNotBeNull();
        provider.GetRequiredService<IQuoteRepository>().ShouldBeOfType<PostgresQuoteRepository>();
    }
}
