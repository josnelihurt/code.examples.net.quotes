using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Auth.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();

    public DependencyInjectionTests()
    {
        _environment.EnvironmentName.Returns(Environments.Development);
    }

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
        services.AddAuthInfrastructure(_environment);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICredentialStore>().ShouldBeOfType<HardcodedCredentialStore>();
        provider.GetRequiredService<ITokenService>().ShouldBeOfType<JwtTokenService>();
    }

    [Fact]
    public void AddAuthInfrastructure_refuses_to_register_the_scaffolding_store_in_production()
    {
        _environment.EnvironmentName.Returns(Environments.Production);

        var services = new ServiceCollection();
        services.AddLogging();

        Should.Throw<InvalidOperationException>(
            () => services.AddAuthInfrastructure(_environment))
            .Message.ShouldContain("Production");
    }
}
