using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Auth.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    // Scopes granted to the demo user; the Quotes API requires quotes:write to create.
    private const string _quotesReadScope = "quotes:read";
    private const string _quotesWriteScope = "quotes:write";

    // Must match JwtAuthExtensions.ScopeClaimType in ServiceDefaults, which owns the
    // policy that consumes these claims.
    private const string _scopeClaimType = "scope";

    private readonly string _audience;
    private readonly int _expiresInSeconds;
    private readonly string _issuer;
    private readonly SymmetricSecurityKey _key;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _logger = logger;
        var jwt = configuration.GetSection("Jwt");
        var signingKey = jwt["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is required");

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _issuer = jwt["Issuer"] ?? "auth-api";
        _audience = jwt["Audience"] ?? "aspire-quotes-poc";
        _expiresInSeconds = int.TryParse(jwt["ExpiresInSeconds"], out var seconds) ? seconds : 3600;
    }

    public string CreateToken(string username, out int expiresInSeconds)
    {
        expiresInSeconds = _expiresInSeconds;
        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims:
            [
                new Claim(ClaimTypes.Name, username),
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(_scopeClaimType, _quotesReadScope),
                new Claim(_scopeClaimType, _quotesWriteScope)
            ],
            expires: DateTime.UtcNow.AddSeconds(_expiresInSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ValidateResult ValidateToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new ValidateResult(false, null);
        }

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            var username = principal.Identity?.Name
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return string.IsNullOrWhiteSpace(username)
                ? new ValidateResult(false, null)
                : new ValidateResult(true, username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed");
            return new ValidateResult(false, null);
        }
    }
}
