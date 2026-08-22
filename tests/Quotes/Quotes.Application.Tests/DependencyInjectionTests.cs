using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Quotes.Application.Abstractions;
using Quotes.Domain.Abstractions;

namespace Quotes.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddQuotesApplication_resolves_every_use_case()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IQuoteRepository>());
        services.AddQuotesApplication();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IGetRandomQuoteUseCase>().ShouldBeOfType<GetRandomQuoteUseCase>();
        provider.GetRequiredService<IGetQuoteByIdUseCase>().ShouldBeOfType<GetQuoteByIdUseCase>();
        provider.GetRequiredService<ICreateQuoteUseCase>().ShouldBeOfType<CreateQuoteUseCase>();
    }
}
