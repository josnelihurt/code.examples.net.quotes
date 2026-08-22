using Microsoft.Extensions.DependencyInjection;
using Quotes.Application.Abstractions;

namespace Quotes.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application layer's own services. Each layer owns its registrations;
    /// the API host composes them and is the single composition root.
    /// </summary>
    public static IServiceCollection AddQuotesApplication(this IServiceCollection services)
    {
        services.AddScoped<IGetRandomQuoteUseCase, GetRandomQuoteUseCase>();
        services.AddScoped<IGetQuoteByIdUseCase, GetQuoteByIdUseCase>();
        services.AddScoped<IListQuotesUseCase, ListQuotesUseCase>();
        services.AddScoped<ICreateQuoteUseCase, CreateQuoteUseCase>();
        return services;
    }
}
