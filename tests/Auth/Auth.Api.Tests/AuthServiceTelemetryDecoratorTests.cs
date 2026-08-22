using System.Diagnostics.Metrics;
using AspireQuotesPoc.ServiceDefaults.Telemetry;
using Auth.Api.Telemetry;
using Auth.Application;
using Auth.Application.Abstractions;
using Auth.Domain.Abstractions;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Auth.Api.Tests;

public class AuthServiceTelemetryDecoratorTests
{
    private readonly IAuthService _inner = Substitute.For<IAuthService>();

    [Fact]
    public async Task Login_records_success_and_failure_and_passes_the_result_through()
    {
        ErrorOr<LoginResult> granted = new LoginResult("issued-token", "jrb", 3600);
        ErrorOr<LoginResult> rejected = AuthErrors.InvalidCredentials;
        _inner.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(granted, rejected);

        var sut = new AuthServiceTelemetry(_inner);

        var first = await CaptureAsync(AppMetrics.AuthLoginCount,
            () => sut.LoginAsync(new LoginRequest("jrb", "secret"), TestContext.Current.CancellationToken));
        var second = await CaptureAsync(AppMetrics.AuthLoginCount,
            () => sut.LoginAsync(new LoginRequest("jrb", "wrong"), TestContext.Current.CancellationToken));

        first.Measurements.ShouldBe([(1L, "success")]);
        first.Result.Value.Username.ShouldBe("jrb");
        second.Measurements.ShouldBe([(1L, "failure")]);
        second.Result.FirstError.Code.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Validate_records_success_and_failure_and_passes_the_result_through()
    {
        _inner.ValidateAsync("token", Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(true, "jrb"), new ValidateResult(false, null));

        var sut = new AuthServiceTelemetry(_inner);

        var first = await CaptureAsync(AppMetrics.AuthValidateCount,
            () => sut.ValidateAsync("token", TestContext.Current.CancellationToken));
        var second = await CaptureAsync(AppMetrics.AuthValidateCount,
            () => sut.ValidateAsync("token", TestContext.Current.CancellationToken));

        first.Measurements.ShouldBe([(1L, "success")]);
        first.Result.Username.ShouldBe("jrb");
        second.Measurements.ShouldBe([(1L, "failure")]);
        second.Result.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task Logging_decorator_passes_results_through_untouched()
    {
        ErrorOr<LoginResult> granted = new LoginResult("issued-token", "jrb", 3600);
        _inner.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(granted);
        _inner.ValidateAsync("token", Arg.Any<CancellationToken>()).Returns(new ValidateResult(true, "jrb"));

        var sut = new AuthServiceLogging(_inner, NullLogger<AuthServiceLogging>.Instance);

        (await sut.LoginAsync(new LoginRequest("jrb", "secret"), TestContext.Current.CancellationToken))
            .Value.AccessToken.ShouldBe("issued-token");
        (await sut.ValidateAsync("token", TestContext.Current.CancellationToken))
            .Username.ShouldBe("jrb");
    }

    [Fact]
    public void AddAuthServiceTelemetry_resolves_a_singleton_decorator_chain()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<ICredentialStore>());
        services.AddSingleton(Substitute.For<ITokenService>());
        services.AddAuthApplication();
        services.AddAuthServiceTelemetry();

        using var provider = services.BuildServiceProvider();

        var auth = provider.GetRequiredService<IAuthService>();
        auth.ShouldBeOfType<AuthServiceTelemetry>();
        provider.GetRequiredService<IAuthService>().ShouldBeSameAs(auth);

        // The bare service stays resolvable for the chain's inner leg.
        provider.GetRequiredService<AuthService>().ShouldNotBeNull();
    }

    /// <summary>
    /// Runs <paramref name="act"/> under a meter listener scoped to <paramref name="counter"/>,
    /// capturing the (value, outcome) measurements it published (pattern from AppMetricsTests).
    /// </summary>
    private static async Task<(List<(long Value, string? Outcome)> Measurements, T Result)> CaptureAsync<T>(
        Counter<long> counter, Func<Task<T>> act)
    {
        var measurements = new List<(long Value, string? Outcome)>();
        _ = counter; // Force the static counter to publish before the listener starts.

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument, counter))
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
        var result = await act();
        return (measurements, result);
    }
}
