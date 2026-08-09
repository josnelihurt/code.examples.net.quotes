namespace AspireQuotesPoc.Resilience;

/// <summary>
/// Tuning for the Quotes -&gt; Auth HttpClient pipeline. Bind from configuration section
/// <c>Resilience:AuthValidate</c> to change it without a rebuild.
/// </summary>
public sealed class AuthResilienceOptions
{
    public const string SectionName = "Resilience:AuthValidate";

    public int MaxRetryAttempts { get; init; } = 3;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerMinimumThroughput { get; init; } = 5;

    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
