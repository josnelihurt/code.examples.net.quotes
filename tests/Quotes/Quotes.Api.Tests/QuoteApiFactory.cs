using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Quotes.Api.Tests;

/// <summary>
/// Boots the real <c>Program</c> (middleware order, validators, DI, real use cases and
/// repository) so the composition root itself is under test. The signing key is random
/// per factory instance; tests never depend on a committed key.
/// </summary>
/// <remarks>
/// The catalog is real too: one PostgreSQL container backs every factory in the serialized
/// web-host collection, and each factory migrates + seeds its own database on it — the same
/// isolation the per-factory in-memory catalog used to give, without a second boot mode.
/// </remarks>
public sealed class QuoteApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Matches the tag Aspire.Hosting.PostgreSQL 13.4.6 pins, so all boot paths share one image.
    private const string _image = "docker.io/library/postgres:18.3";

    private static readonly PostgreSqlContainer _container = new PostgreSqlBuilder(_image).Build();

    // Started once (static ctor), awaited by every factory.
    private static readonly Task _containerStarted = _container.StartAsync();

    private string _connectionString = null!;

    public string SigningKey { get; } = $"integration-key-{Guid.NewGuid():N}{Guid.NewGuid():N}";

    public async ValueTask InitializeAsync()
    {
        await _containerStarted;
        var database = $"quotes_api_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"create database \"{database}\"";
            await command.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:quotesdb", _connectionString);
    }

    public string CreateToken(params string[] scopes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "jrb"),
            new(JwtRegisteredClaimNames.Sub, "jrb")
        };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));
        var token = new JwtSecurityToken(
            issuer: JwtAuthExtensions.DefaultIssuer,
            audience: JwtAuthExtensions.DefaultAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
