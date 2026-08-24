using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace ServiceDefaults.Tests;

public class JwtAuthExtensionsTests
{
    [Fact]
    public async Task AddStandardJwtAuthentication_registers_jwt_bearer_and_authorization()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration["Jwt:SigningKey"] = "unit-test-signing-key-that-is-long-enough-1234567890";
        builder.Configuration["Jwt:Issuer"] = JwtAuthExtensions.DefaultIssuer;
        builder.Configuration["Jwt:Audience"] = JwtAuthExtensions.DefaultAudience;

        builder.AddStandardJwtAuthentication();

        await using var provider = builder.Services.BuildServiceProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme))
            .ShouldNotBeNull();

        provider.GetService<IAuthorizationService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddStandardJwtAuthentication_requires_a_signing_key()
    {
        var builder = WebApplication.CreateSlimBuilder();

        var act = () => builder.AddStandardJwtAuthentication();

        Should.Throw<InvalidOperationException>(act)
            .Message.ShouldContain(JwtAuthExtensions.SigningKeyKey);
    }

    [Fact]
    public void AddStandardJwtAuthentication_rejects_the_development_key_in_production()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration["Jwt:SigningKey"] = JwtAuthExtensions.DevelopmentSigningKey;

        var act = () => builder.AddStandardJwtAuthentication();

        Should.Throw<InvalidOperationException>(act)
            .Message.ShouldContain("development key");
    }

    [Fact]
    public async Task AddStandardJwtAuthentication_accepts_a_real_key_in_production()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration["Jwt:SigningKey"] = "a-production-grade-secret-key-of-sufficient-length";

        builder.AddStandardJwtAuthentication(("seed:read", "seed:read"), ("seed:write", "seed:write"));

        var policyProvider = builder.Services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationPolicyProvider>();
        (await policyProvider.GetPolicyAsync("seed:read")).ShouldNotBeNull();
        (await policyProvider.GetPolicyAsync("seed:write")).ShouldNotBeNull();
    }

    [Fact]
    public async Task AddStandardJwtAuthentication_carries_no_policies_when_none_are_passed()
    {
        // The kit holds no context vocabulary: with no scopes declared at composition there
        // is nothing to authorize against beyond authentication.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration["Jwt:SigningKey"] = "unit-test-signing-key-that-is-long-enough-1234567890";

        builder.AddStandardJwtAuthentication();

        var policyProvider = builder.Services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationPolicyProvider>();
        (await policyProvider.GetPolicyAsync("quotes:read")).ShouldBeNull();
    }
}
