using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure.Tests;

/// <summary>
/// Runs the repository contract suite against the real PostgreSQL adapter. The contract
/// suite starts every test from an empty catalog, so the migrated (seeded) database is
/// truncated first; the migration itself and the seed it ships are proven separately by
/// <see cref="QuotesDbContextMigrationTests"/>.
/// </summary>
public sealed class PostgresQuoteRepositoryTests : QuoteRepositoryContractTests, IAsyncLifetime
{
    private string _connectionString = null!;
    private ServiceProvider _provider = new ServiceCollection().BuildServiceProvider();

    public async ValueTask InitializeAsync() =>
        _connectionString = await PostgresTestDatabase.CreateAsync();

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    protected override async Task<IQuoteRepository> CreateRepositoryAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<QuotesDbContext>(options => options.UseNpgsql(_connectionString));
        services.AddScoped<IQuoteRepository, PostgresQuoteRepository>();
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("truncate table quotes");

        return _provider.GetRequiredService<IQuoteRepository>();
    }
}
