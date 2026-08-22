using Microsoft.Extensions.DependencyInjection;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Abstractions;

namespace Quotes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the persistence adapters only. Use cases are registered by
    /// <c>AddQuotesApplication</c>; the API host composes both.
    /// </summary>
    public static IServiceCollection AddQuotesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IQuoteSelector, RandomQuoteSelector>();
        services.AddSingleton<IQuoteRepository, InMemoryQuoteRepository>();
        return services;
    }
}
