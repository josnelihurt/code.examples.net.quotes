using AspireQuotesPoc.ServiceDefaults.Telemetry;
using ErrorOr;

namespace ServiceDefaults.Tests;

public class UseCaseTelemetryTests
{
    [Theory]
    [InlineData(ErrorType.Validation, "invalid")]
    [InlineData(ErrorType.Conflict, "conflict")]
    [InlineData(ErrorType.NotFound, "not_found")]
    [InlineData(ErrorType.Failure, "error")]
    [InlineData(ErrorType.Unauthorized, "error")]
    [InlineData(ErrorType.Forbidden, "error")]
    [InlineData(ErrorType.Unexpected, "error")]
    public void Outcome_maps_error_types_onto_the_documented_vocabulary(ErrorType type, string expected)
    {
        UseCaseTelemetry.Outcome(type).ShouldBe(expected);
    }
}
