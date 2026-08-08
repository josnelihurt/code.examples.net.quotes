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
    public static IHttpClientBuilder AddAuthHttpClientResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("auth-validate", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(15)
            });

            pipeline.AddTimeout(TimeSpan.FromSeconds(10));
        });

        return builder;
    }
}
