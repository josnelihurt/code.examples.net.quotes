using System.Text;
using System.Text.Json;
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

    public static TBuilder AddStandardJwtAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var jwt = builder.Configuration.GetSection(JwtSectionName);
        var signingKey = jwt["SigningKey"]
            ?? throw new InvalidOperationException($"{SigningKeyKey} is required");

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
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(
                            JsonSerializer.Serialize(new { error = "Unauthorized" }),
                            context.HttpContext.RequestAborted);
                    }
                };
            });

        builder.Services.AddAuthorization();
        return builder;
    }

    public static WebApplication UseStandardAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
