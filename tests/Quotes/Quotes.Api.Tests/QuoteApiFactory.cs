using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Quotes.Api.Tests;

/// <summary>
/// Boots the real <c>Program</c> (middleware order, validators, DI, real use cases and
/// repository) so the composition root itself is under test. The signing key is random
/// per factory instance; tests never depend on a committed key.
/// </summary>
public sealed class QuoteApiFactory : WebApplicationFactory<Program>
{
    public string SigningKey { get; } = $"integration-key-{Guid.NewGuid():N}{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", "auth-api");
        builder.UseSetting("Jwt:Audience", "aspire-quotes-poc");
    }

    public string CreateToken(bool withWriteScope)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "jrb"),
            new(JwtRegisteredClaimNames.Sub, "jrb")
        };
        if (withWriteScope)
        {
            claims.Add(new Claim("scope", "quotes:read"));
            claims.Add(new Claim("scope", "quotes:write"));
        }

        var token = new JwtSecurityToken(
            issuer: "auth-api",
            audience: "aspire-quotes-poc",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
