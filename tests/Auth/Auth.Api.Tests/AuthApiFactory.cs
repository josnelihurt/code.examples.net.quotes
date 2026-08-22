using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Auth.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Tests;

/// <summary>
/// Boots the real <c>Program</c> (middleware order, validators, DI, real credential store
/// and token service) so the Auth composition root is under test, mirroring the Quotes
/// suite. The signing key is random per factory instance.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public string SigningKey { get; } = $"auth-integration-key-{Guid.NewGuid():N}{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Jwt:Issuer", "auth-api");
        builder.UseSetting("Jwt:Audience", "aspire-quotes-poc");
    }

    public async Task<string> IssueTokenAsync()
    {
        var tokenService = Services.GetRequiredService<ITokenService>();
        var issued = await tokenService.CreateTokenAsync("jrb", CancellationToken.None);
        return issued.AccessToken;
    }

    /// <summary>Mints a token signed with the factory key but outside the service, for signature-mismatch tests.</summary>
    public string IssueForeignToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes($"foreign-{Guid.NewGuid():N}{Guid.NewGuid():N}"));
        var token = new JwtSecurityToken(
            issuer: "auth-api",
            audience: "aspire-quotes-poc",
            claims: [new Claim(ClaimTypes.Name, "jrb"), new Claim(JwtRegisteredClaimNames.Sub, "jrb")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
