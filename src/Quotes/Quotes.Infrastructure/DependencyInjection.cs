using System.Diagnostics.CodeAnalysis;
using AspireQuotesPoc.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quotes.Application;
using Quotes.Domain;

namespace Quotes.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Default resolves through Aspire service discovery; override with <c>Services:AuthApi:BaseAddress</c>.
    /// </summary>
    [SuppressMessage(
        "Security",
        "S5332:Using clear-text protocols is security-sensitive",
        Justification = "Logical Aspire service-discovery name, rewritten to the real scheme and port at runtime.")]
    internal const string DefaultAuthApiBaseAddress = "http://auth-api";

    internal const string AuthApiBaseAddressKey = "Services:AuthApi:BaseAddress";

    public static IServiceCollection AddQuotesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var authApiBaseAddress = configuration[AuthApiBaseAddressKey];
        if (string.IsNullOrWhiteSpace(authApiBaseAddress))
        {
            authApiBaseAddress = DefaultAuthApiBaseAddress;
        }

        var resilience = configuration.GetSection(AuthResilienceOptions.SectionName)
            .Get<AuthResilienceOptions>();

        services.AddSingleton<IQuoteSelector, RandomQuoteSelector>();
        services.AddSingleton<IQuoteRepository, InMemoryQuoteRepository>();
        services.AddScoped<IGetRandomQuoteUseCase, GetRandomQuoteUseCase>();
        services.AddHttpClient<IAuthValidationClient, AuthValidationClient>(client =>
            {
                client.BaseAddress = new Uri(authApiBaseAddress);
            })
            .AddAuthHttpClientResilience(resilience);
        return services;
    }
}
