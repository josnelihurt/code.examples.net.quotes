using ErrorOr;

namespace AspireQuotesPoc.ServiceDefaults.Telemetry;

/// <summary>
/// Outcome vocabulary for the quote counters. The values are public metric contract
/// (see docs/observability.md); auth counters deliberately keep plain success/failure.
/// </summary>
public static class UseCaseTelemetry
{
    public static string Outcome(ErrorType type) => type switch
    {
        ErrorType.Validation => "invalid",
        ErrorType.Conflict => "conflict",
        ErrorType.NotFound => "not_found",
        _ => "error"
    };
}
