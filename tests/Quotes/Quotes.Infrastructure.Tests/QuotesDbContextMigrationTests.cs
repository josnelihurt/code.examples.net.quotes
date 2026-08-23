using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure.Tests;

/// <summary>
/// Proves the migration end to end: an empty database that migrates immediately holds
/// exactly the shipped catalog, in the stable order the BDD and e2e suites assert on.
/// The repository contract suite lives in <c>PostgresQuoteRepositoryTests</c>, added with
/// the adapter in the next layer of this stack (a cref would not resolve until then).
/// </summary>
public sealed class QuotesDbContextMigrationTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private ServiceProvider _provider = new ServiceCollection().BuildServiceProvider();

    public async ValueTask InitializeAsync() =>
        _connectionString = await PostgresTestDatabase.CreateAsync();

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task Migrating_an_empty_database_ships_the_seeded_catalog()
    {
        var services = new ServiceCollection();
        services.AddDbContext<QuotesDbContext>(options => options.UseNpgsql(_connectionString));
        _provider = services.BuildServiceProvider();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = _provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
            var rows = await context.Quotes
                .OrderBy(record => record.CreatedAtUtc)
                .ThenBy(record => record.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            // The same eight quotes (same ids) the BDD and Playwright suites assert on:
            // ids 1..8 share a timestamp and tie-break lexically.
            rows.Select(record => record.Id).ShouldBe(["1", "2", "3", "4", "5", "6", "7", "8"]);
            rows.First().Author.ShouldBe("Leonardo da Vinci");
            rows.Single(record => record.Id == "7").Author.ShouldBe("Harold Abelson");
        }
    }
}
