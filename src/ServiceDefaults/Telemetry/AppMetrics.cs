using System.Diagnostics.Metrics;

namespace AspireQuotesPoc.Telemetry;

public static class AppMetrics
{
    public const string MeterName = "AspireQuotesPoc";

    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AuthLoginCount =
        Meter.CreateCounter<long>("auth.login.count", description: "Auth login attempts");

    public static readonly Counter<long> AuthValidateCount =
        Meter.CreateCounter<long>("auth.validate.count", description: "Auth token validations");

    public static readonly Counter<long> QuotesRandomCount =
        Meter.CreateCounter<long>("quotes.random.count", description: "Random quote requests");

    public static void Record(Counter<long> counter, string outcome) =>
        counter.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
