using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddAuthInfrastructure_resolves_the_infrastructure_adapters()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "unit-test-signing-key-that-is-long-enough-1234567890"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddAuthInfrastructure();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICredentialStore>().ShouldBeOfType<HardcodedCredentialStore>();
        provider.GetRequiredService<ITokenService>().ShouldBeOfType<JwtTokenService>();
    }
}
