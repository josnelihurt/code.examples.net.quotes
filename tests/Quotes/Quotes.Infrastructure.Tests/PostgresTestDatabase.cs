using Npgsql;
using Testcontainers.PostgreSql;

namespace Quotes.Infrastructure.Tests;

/// <summary>
/// One PostgreSQL container per test run; every test gets its own database on it, so
/// migrations run from scratch without cross-test bleed.
/// </summary>
internal static class PostgresTestDatabase
{
    // The image is the repo's single copy of the tag Aspire.Hosting.PostgreSQL pins
    // (scripts/images.env, shared with e2e and CI so all boot paths run one image);
    // a POSTGRES_IMAGE env var overrides it for local experiments.
    private static readonly string _image = ResolveImage();

    private static string ResolveImage()
    {
        var overrideImage = Environment.GetEnvironmentVariable("POSTGRES_IMAGE");
        if (!string.IsNullOrWhiteSpace(overrideImage))
        {
            return overrideImage;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "images.env")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "scripts/images.env not found above the test output directory; the container image pin cannot be resolved.");
        }

        foreach (var line in File.ReadLines(Path.Combine(directory.FullName, "scripts", "images.env")))
        {
            var entry = line.Split('=', 2);
            if (entry.Length == 2 && entry[0].Trim() == "POSTGRES_IMAGE")
            {
                return entry[1].Trim();
            }
        }

        throw new InvalidOperationException(
            "scripts/images.env has no POSTGRES_IMAGE entry; the container image pin cannot be resolved.");
    }

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
