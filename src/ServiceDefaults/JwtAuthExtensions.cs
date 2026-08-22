using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Scope-based policies: read endpoints require <c>quotes:read</c>; the create
    /// endpoint requires <c>quotes:write</c> (any authenticated token can do nothing
    /// beyond proving it is valid).
    /// </summary>
    public const string ReadQuotesPolicy = "quotes:read";
    public const string ReadQuotesScope = "quotes:read";
    public const string WriteQuotesPolicy = "quotes:write";
    public const string WriteQuotesScope = "quotes:write";

    public static TBuilder AddStandardJwtAuthentication<TBuilder>(this TBuilder builder)
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
                    // the framework's default empty 401, so clients parse one error shape.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var response = context.Response;
                        response.StatusCode = StatusCodes.Status401Unauthorized;
                        response.ContentType = "application/problem+json";
                        response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
                            ? "Bearer"
                            : "Bearer error=\"invalid_token\"";

                        var problem = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "A valid bearer token is required.",
                            Extensions = { ["correlationId"] = context.HttpContext.GetCorrelationId() }
                        };

                        await response.WriteAsync(
                            JsonSerializer.Serialize(
                                problem,
                                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                            context.HttpContext.RequestAborted);
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(ReadQuotesPolicy, policy =>
                policy.RequireClaim(ScopeClaimType, ReadQuotesScope));
            options.AddPolicy(WriteQuotesPolicy, policy =>
                policy.RequireClaim(ScopeClaimType, WriteQuotesScope));
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
