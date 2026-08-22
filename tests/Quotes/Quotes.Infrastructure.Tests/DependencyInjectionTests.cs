using Microsoft.Extensions.DependencyInjection;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;

namespace Quotes.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddQuotesInfrastructure_resolves_the_persistence_adapters()
    {
        var services = new ServiceCollection();
        services.AddQuotesInfrastructure();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IQuoteSelector>().ShouldBeOfType<RandomQuoteSelector>();
        provider.GetRequiredService<IQuoteRepository>().ShouldBeOfType<InMemoryQuoteRepository>();
    }
}
