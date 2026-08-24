using System.Text;
using System.Text.Json;
using AspireQuotesPoc.ServiceDefaults.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

public static class JwtAuthExtensions
{
    public const string JwtSectionName = "Jwt";
    public const string SigningKeyKey = "Jwt:SigningKey";
    public const string DefaultIssuer = "auth-api";
    public const string DefaultAudience = "aspire-quotes-poc";

    /// <summary>
    /// The documented development signing key (user-secrets in Development). Production
    /// startup fails if it is ever the configured key.
    /// </summary>
    public const string DevelopmentSigningKey = "AspireQuotesPoc-Dev-Signing-Key-32chars!";

    public const string ScopeClaimType = "scope";

    /// <summary>errorCode carried by the 401 problem when no token was presented.</summary>
    public const string TokenMissingErrorCode = "auth.token_missing";

    /// <summary>errorCode carried by the 401 problem when the token failed validation.</summary>
    public const string TokenInvalidErrorCode = "auth.token_invalid";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Registers JwtBearer validation plus one authorization policy per scope the host
    /// passes in. The kit carries no context vocabulary: each API declares its own scopes
    /// at composition — for example <c>AddStandardJwtAuthentication(("quotes:read",
    /// "quotes:read"), ("quotes:write", "quotes:write"))</c> — so a second bounded context
    /// grows authorization without editing the shared kit.
    /// </summary>
    public static TBuilder AddStandardJwtAuthentication<TBuilder>(
        this TBuilder builder,
        params (string Policy, string Scope)[] scopePolicies)
        where TBuilder : IHostApplicationBuilder
    {
        var jwt = builder.Configuration.GetSection(JwtSectionName);
        var signingKey = jwt["SigningKey"]
            ?? throw new InvalidOperationException($"{SigningKeyKey} is required");

        if (builder.Environment.IsProduction() && signingKey == DevelopmentSigningKey)
        {
            throw new InvalidOperationException(
                $"{SigningKeyKey} is set to the public development key; configure a real secret before running in Production.");
        }

        var issuer = jwt["Issuer"] ?? DefaultIssuer;
        var audience = jwt["Audience"] ?? DefaultAudience;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    // RFC 9457 envelope (and the RFC 9110 WWW-Authenticate header) instead of
                    // the framework's default empty 401, built by the shared problem factory
                    // so clients parse one error shape.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var response = context.Response;
                        response.StatusCode = StatusCodes.Status401Unauthorized;
                        response.ContentType = "application/problem+json";
                        var tokenInvalid = context.AuthenticateFailure is not null;
                        response.Headers.WWWAuthenticate = tokenInvalid
                            ? "Bearer error=\"invalid_token\""
                            : "Bearer";

                        var problem = ProblemDetailsBuilder.Build(
                            StatusCodes.Status401Unauthorized,
                            tokenInvalid ? TokenInvalidErrorCode : TokenMissingErrorCode,
                            "A valid bearer token is required.",
                            context.HttpContext);

                        await response.WriteAsync(
                            JsonSerializer.Serialize(problem, _jsonOptions),
                            context.HttpContext.RequestAborted);
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            foreach (var (policy, scope) in scopePolicies)
            {
                options.AddPolicy(policy, builder => builder.RequireClaim(ScopeClaimType, scope));
            }
        });
        return builder;
    }

    public static WebApplication UseStandardAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
