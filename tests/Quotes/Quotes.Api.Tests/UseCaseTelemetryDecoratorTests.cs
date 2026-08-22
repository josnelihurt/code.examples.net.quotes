using System.Diagnostics.Metrics;
using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quotes.Api.Telemetry;
using Quotes.Application;
using Quotes.Application.Abstractions;
using Quotes.Domain;
using Quotes.Domain.Abstractions;

namespace Quotes.Api.Tests;

public class UseCaseTelemetryDecoratorTests
{
    private static readonly QuoteDto _sampleQuote =
        new("7", "Programs must be written for people to read.", "Harold Abelson");

    private readonly IGetRandomQuoteUseCase _random = Substitute.For<IGetRandomQuoteUseCase>();
    private readonly IGetQuoteByIdUseCase _getById = Substitute.For<IGetQuoteByIdUseCase>();
    private readonly ICreateQuoteUseCase _create = Substitute.For<ICreateQuoteUseCase>();

    [Fact]
    public async Task Random_decorator_records_success_and_not_found_and_passes_the_result_through()
    {
        ErrorOr<QuoteDto> sample = _sampleQuote;
        ErrorOr<QuoteDto> missing = QuoteErrors.NotFound;
        _random.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(sample, missing);

        var sut = new GetRandomQuoteUseCaseTelemetry(_random);

        var first = await CaptureAsync(AppMetrics.QuotesRandomCount,
            () => sut.ExecuteAsync(TestContext.Current.CancellationToken));
        var second = await CaptureAsync(AppMetrics.QuotesRandomCount,
            () => sut.ExecuteAsync(TestContext.Current.CancellationToken));

        first.Measurements.ShouldBe([(1L, "success")]);
        first.Result.Value.Id.ShouldBe(_sampleQuote.Id);
        second.Measurements.ShouldBe([(1L, "not_found")]);
        second.Result.FirstError.Code.ShouldBe("quote.not_found");
    }

    [Fact]
    public async Task GetById_decorator_records_success_and_not_found_and_passes_the_result_through()
    {
        ErrorOr<QuoteDto> sample = _sampleQuote;
        ErrorOr<QuoteDto> missing = QuoteErrors.NotFound;
        _getById.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(sample, missing);

        var sut = new GetQuoteByIdUseCaseTelemetry(_getById);

        var first = await CaptureAsync(AppMetrics.QuotesGetByIdCount,
            () => sut.ExecuteAsync(_sampleQuote.Id, TestContext.Current.CancellationToken));
        var second = await CaptureAsync(AppMetrics.QuotesGetByIdCount,
            () => sut.ExecuteAsync("missing", TestContext.Current.CancellationToken));

        first.Measurements.ShouldBe([(1L, "success")]);
        first.Result.Value.Id.ShouldBe(_sampleQuote.Id);
        second.Measurements.ShouldBe([(1L, "not_found")]);
        second.Result.FirstError.Code.ShouldBe("quote.not_found");
    }

    [Fact]
    public async Task Create_decorator_records_success_and_passes_the_quote_through()
    {
        ErrorOr<QuoteDto> created = _sampleQuote;
        _create.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(created);

        var sut = new CreateQuoteUseCaseTelemetry(_create);

        var outcome = await CaptureAsync(AppMetrics.QuotesCreateCount,
            () => sut.ExecuteAsync(
                new CreateQuoteCommand(_sampleQuote.Text, _sampleQuote.Author),
                TestContext.Current.CancellationToken));

        outcome.Measurements.ShouldBe([(1L, "success")]);
        outcome.Result.Value.Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public async Task Create_decorator_maps_error_types_onto_the_documented_outcomes()
    {
        var cases = new (Error Error, string Outcome)[]
        {
            (Error.Validation("quote.rejected", "Rejected."), "invalid"),
            (Error.Conflict("quote.rejected", "Rejected."), "conflict"),
            (Error.NotFound("quote.rejected", "Rejected."), "not_found"),
            (Error.Unexpected("quote.rejected", "Rejected."), "error")
        };

        foreach (var (error, expectedOutcome) in cases)
        {
            ErrorOr<QuoteDto> rejected = error;
            _create.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(rejected);

            var sut = new CreateQuoteUseCaseTelemetry(_create);

            var outcome = await CaptureAsync(AppMetrics.QuotesCreateCount,
                () => sut.ExecuteAsync(
                    new CreateQuoteCommand(_sampleQuote.Text, _sampleQuote.Author),
                    TestContext.Current.CancellationToken));

            outcome.Measurements.ShouldBe([(1L, expectedOutcome)], expectedOutcome);
            outcome.Result.FirstError.Code.ShouldBe("quote.rejected");
        }
    }

    [Fact]
    public async Task Logging_decorators_pass_the_result_through_untouched()
    {
        ErrorOr<QuoteDto> sample = _sampleQuote;
        _random.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(sample);
        _getById.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(sample);
        _create.ExecuteAsync(Arg.Any<CreateQuoteCommand>(), Arg.Any<CancellationToken>()).Returns(sample);

        var random = new GetRandomQuoteUseCaseLogging(_random, NullLogger<GetRandomQuoteUseCaseLogging>.Instance);
        var getById = new GetQuoteByIdUseCaseLogging(_getById, NullLogger<GetQuoteByIdUseCaseLogging>.Instance);
        var create = new CreateQuoteUseCaseLogging(_create, NullLogger<CreateQuoteUseCaseLogging>.Instance);

        (await random.ExecuteAsync(TestContext.Current.CancellationToken)).Value.Id.ShouldBe(_sampleQuote.Id);
        (await getById.ExecuteAsync(_sampleQuote.Id, TestContext.Current.CancellationToken)).Value.Id
            .ShouldBe(_sampleQuote.Id);
        (await create.ExecuteAsync(
                new CreateQuoteCommand(_sampleQuote.Text, _sampleQuote.Author),
                TestContext.Current.CancellationToken))
            .Value.Id.ShouldBe(_sampleQuote.Id);
    }

    [Fact]
    public void AddQuotesUseCaseTelemetry_resolves_each_use_case_as_the_telemetry_decorator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IQuoteRepository>());
        services.AddQuotesApplication();
        services.AddQuotesUseCaseTelemetry();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IGetRandomQuoteUseCase>()
            .ShouldBeOfType<GetRandomQuoteUseCaseTelemetry>();
        scope.ServiceProvider.GetRequiredService<IGetQuoteByIdUseCase>()
            .ShouldBeOfType<GetQuoteByIdUseCaseTelemetry>();
        scope.ServiceProvider.GetRequiredService<ICreateQuoteUseCase>()
            .ShouldBeOfType<CreateQuoteUseCaseTelemetry>();

        // The bare use cases stay resolvable for the chain's inner leg.
        scope.ServiceProvider.GetRequiredService<CreateQuoteUseCase>().ShouldNotBeNull();
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
