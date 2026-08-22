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
    private static readonly JwtSecurityTokenHandler _handler = new();

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
        // These fallbacks mirror JwtAuthExtensions.DefaultIssuer/DefaultAudience in ServiceDefaults;
        // the test suite pins them together so the pair cannot drift silently.
        _issuer = jwt["Issuer"] ?? "auth-api";
        _audience = jwt["Audience"] ?? "aspire-quotes-poc";
        _expiresInSeconds = int.TryParse(jwt["ExpiresInSeconds"], out var seconds) ? seconds : 3600;
    }

    public Task<IssuedToken> CreateTokenAsync(string username, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(JwtRegisteredClaimNames.Sub, username)
        };
        claims.AddRange(scopes.Distinct().Select(scope => new Claim(AuthorizationScopes.ClaimType, scope)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(_expiresInSeconds),
            signingCredentials: credentials);

        return Task.FromResult(new IssuedToken(_handler.WriteToken(token), _expiresInSeconds));
    }

    public Task<ValidateResult> ValidateTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(new ValidateResult(false, null));
        }

        try
        {
            var principal = _handler.ValidateToken(accessToken, new TokenValidationParameters
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

            return Task.FromResult(string.IsNullOrWhiteSpace(username)
                ? new ValidateResult(false, null)
                : new ValidateResult(true, username));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT validation failed");
            return Task.FromResult(new ValidateResult(false, null));
        }
    }
}
