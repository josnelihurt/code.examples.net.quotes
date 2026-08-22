using Microsoft.Extensions.DependencyInjection;
using Quotes.Application;
using Quotes.Application.Abstractions;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;

namespace Quotes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQuotesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IQuoteSelector, RandomQuoteSelector>();
        services.AddSingleton<IQuoteRepository, InMemoryQuoteRepository>();
        services.AddScoped<IGetRandomQuoteUseCase, GetRandomQuoteUseCase>();
        return services;
    }
}
