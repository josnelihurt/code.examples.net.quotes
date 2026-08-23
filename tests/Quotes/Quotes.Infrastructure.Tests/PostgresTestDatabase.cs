using Npgsql;
using Testcontainers.PostgreSql;

namespace Quotes.Infrastructure.Tests;

/// <summary>
/// One PostgreSQL container per test run; every test gets its own database on it, so
/// migrations run from scratch without cross-test bleed.
/// </summary>
internal static class PostgresTestDatabase
{
    // Matches the tag Aspire.Hosting.PostgreSQL 13.4.6 pins, so all boot paths share one image.
    private const string _image = "docker.io/library/postgres:18.3";

    private static readonly PostgreSqlContainer _container = new PostgreSqlBuilder(_image).Build();

    // Started once (static ctor), awaited by every test.
    private static readonly Task _containerStarted = _container.StartAsync();

    public static async Task<string> CreateAsync()
    {
        await _containerStarted;
        var database = $"quotes_test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"create database \"{database}\"";
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database
        }.ConnectionString;
    }
}
