using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quotes.Application;
using Quotes.Domain;

namespace Quotes.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider(string? authApiBaseAddress = null)
    {
        var settings = new Dictionary<string, string?>();
        if (authApiBaseAddress is not null)
        {
            settings[DependencyInjection.AuthApiBaseAddressKey] = authApiBaseAddress;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuotesInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddQuotesInfrastructure_resolves_the_whole_quotes_chain()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IQuoteSelector>().ShouldBeOfType<RandomQuoteSelector>();
        provider.GetRequiredService<IQuoteRepository>().ShouldBeOfType<InMemoryQuoteRepository>();
        provider.GetRequiredService<IGetRandomQuoteUseCase>().ShouldBeOfType<GetRandomQuoteUseCase>();
        provider.GetRequiredService<IAuthValidationClient>().ShouldBeOfType<AuthValidationClient>();
    }

    [Fact]
    public void The_auth_client_targets_service_discovery_by_default()
    {
        using var provider = BuildProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IAuthValidationClient));

        client.BaseAddress.ShouldBe(new Uri(DependencyInjection.DefaultAuthApiBaseAddress));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_configured_base_address_falls_back_to_the_default(string configured)
    {
        using var provider = BuildProvider(configured);

        var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IAuthValidationClient));

        client.BaseAddress.ShouldBe(new Uri(DependencyInjection.DefaultAuthApiBaseAddress));
    }

    [Fact]
    public void A_configured_base_address_overrides_the_default()
    {
        using var provider = BuildProvider("http://localhost:5199");

        var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IAuthValidationClient));

        client.BaseAddress.ShouldBe(new Uri("http://localhost:5199"));
    }
}
