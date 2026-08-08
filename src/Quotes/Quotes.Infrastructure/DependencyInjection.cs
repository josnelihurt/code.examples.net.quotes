using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quotes.Application;
using Quotes.Domain;

namespace Quotes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQuotesInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IQuoteRepository, InMemoryQuoteRepository>();
        services.AddScoped<IGetRandomQuoteUseCase, GetRandomQuoteUseCase>();
        services.AddHttpClient<IAuthValidationClient, AuthValidationClient>(client =>
            {
                client.BaseAddress = new Uri("http://auth-api");
            })
            .AddAuthHttpClientResilience();
        return services;
    }
}
