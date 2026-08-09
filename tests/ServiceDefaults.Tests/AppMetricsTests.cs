using System.Diagnostics.Metrics;
using AspireQuotesPoc.Telemetry;

namespace ServiceDefaults.Tests;

public class AppMetricsTests
{
    [Fact]
    public void The_meter_is_published_under_the_expected_name()
    {
        AppMetrics.MeterName.ShouldBe("AspireQuotesPoc");
        AppMetrics.Meter.Name.ShouldBe(AppMetrics.MeterName);
    }

    [Theory]
    [InlineData("auth.login.count")]
    [InlineData("auth.validate.count")]
    [InlineData("quotes.random.count")]
    public void Every_counter_is_named_and_described(string expectedName)
    {
        var counter = new[] { AppMetrics.AuthLoginCount, AppMetrics.AuthValidateCount, AppMetrics.QuotesRandomCount }
            .Single(c => c.Name == expectedName);

        counter.Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Record_increments_by_one_and_tags_the_outcome()
    {
        var measurements = new List<(long Value, string? Outcome)>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument, AppMetrics.AuthLoginCount))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    outcome = tag.Value as string;
                }
            }

            measurements.Add((value, outcome));
        });

        listener.Start();

        AppMetrics.Record(AppMetrics.AuthLoginCount, "success");
        AppMetrics.Record(AppMetrics.AuthLoginCount, "failure");

        measurements.ShouldBe([(1L, "success"), (1L, "failure")]);
    }
}
