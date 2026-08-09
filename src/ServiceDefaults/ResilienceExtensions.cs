using AspireQuotesPoc.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Microsoft.Extensions.Hosting;

public static class ResilienceExtensions
{
    /// <summary>
    /// Explicit Polly resilience for Quotes -&gt; Auth HttpClient.
    /// Service defaults intentionally omit the global standard resilience handler
    /// so this client is not double-wrapped.
    /// </summary>
    public static IHttpClientBuilder AddAuthHttpClientResilience(
        this IHttpClientBuilder builder,
        AuthResilienceOptions? options = null)
    {
        var settings = options ?? new AuthResilienceOptions();

        builder.AddResilienceHandler("auth-validate", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = settings.MaxRetryAttempts,
                Delay = settings.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = settings.CircuitBreakerSamplingDuration,
                FailureRatio = settings.CircuitBreakerFailureRatio,
                MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                BreakDuration = settings.CircuitBreakerBreakDuration
            });

            pipeline.AddTimeout(settings.Timeout);
        });

        return builder;
    }
}
