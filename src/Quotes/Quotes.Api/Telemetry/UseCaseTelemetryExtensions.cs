using Quotes.Application;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Telemetry;

public static class UseCaseTelemetryExtensions
{
    /// <summary>
    /// Wraps every quote use case in the telemetry/logging decorator chain
    /// (telemetry outermost, then logging, then the use case). These registrations
    /// resolve ahead of <c>AddQuotesApplication</c>'s because the last registration
    /// of a service type wins.
    /// </summary>
    public static IServiceCollection AddQuotesUseCaseTelemetry(this IServiceCollection services)
    {
        services.AddScoped<GetRandomQuoteUseCase>();
        services.AddScoped<IGetRandomQuoteUseCase>(sp => new GetRandomQuoteUseCaseTelemetry(
            new GetRandomQuoteUseCaseLogging(
                sp.GetRequiredService<GetRandomQuoteUseCase>(),
                sp.GetRequiredService<ILogger<GetRandomQuoteUseCaseLogging>>())));

        services.AddScoped<GetQuoteByIdUseCase>();
        services.AddScoped<IGetQuoteByIdUseCase>(sp => new GetQuoteByIdUseCaseTelemetry(
            new GetQuoteByIdUseCaseLogging(
                sp.GetRequiredService<GetQuoteByIdUseCase>(),
                sp.GetRequiredService<ILogger<GetQuoteByIdUseCaseLogging>>())));

        services.AddScoped<CreateQuoteUseCase>();
        services.AddScoped<ICreateQuoteUseCase>(sp => new CreateQuoteUseCaseTelemetry(
            new CreateQuoteUseCaseLogging(
                sp.GetRequiredService<CreateQuoteUseCase>(),
                sp.GetRequiredService<ILogger<CreateQuoteUseCaseLogging>>())));

        return services;
    }
}
