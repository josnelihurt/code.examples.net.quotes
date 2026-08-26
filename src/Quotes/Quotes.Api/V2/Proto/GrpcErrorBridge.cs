using ErrorOr;
using Grpc.Core;

namespace Quotes.Api.V2.Proto;

/// <summary>
/// Carries ErrorOr failures across the generated gRPC service boundary without losing a
/// single field. The v2 service implementation throws <see cref="RpcException"/> (the only
/// error channel a generated service base offers); the HTTP adapter catches it and rebuilds
/// the identical <c>List&lt;Error&gt;</c> so the shared ProblemDetails factory renders the
/// exact problem body v0/v1 produce. Encode and decode are exercised on every v2 call, which
/// is what keeps the round trip honest.
/// </summary>
internal static class GrpcErrorBridge
{
    /// <summary>Repeated metadata key; one entry per Error, in original order.</summary>
    internal const string MetadataKey = "quotes-error";

    /// <summary>
    /// Encodes errors as repeated metadata entries shaped <c>grpcStatusCode|errorCode|description</c>.
    /// The description is last so a pipe inside it cannot shift the split. Metadata values must
    /// be ASCII; the error catalog today is plain ASCII and parity tests guard that.
    /// </summary>
    internal static RpcException ToRpcException(List<Error> errors)
    {
        var primary = errors.Count > 0 ? errors[0] : Error.Unexpected("error.unknown", "An unexpected error occurred.");
        var metadata = new Metadata();
        foreach (var error in errors)
        {
            // The numeric gRPC status code (not its name) so the decoder's int parse holds.
            metadata.Add(MetadataKey, $"{(int)ToGrpcStatusCode(error.Type)}|{error.Code}|{error.Description}");
        }

        return new RpcException(new Status(ToGrpcStatusCode(primary.Type), primary.Description), metadata);
    }

    /// <summary>Reverses <see cref="ToRpcException"/>; entries missing or unreadable fail closed to the exception itself.</summary>
    internal static List<Error> ToErrors(RpcException exception)
    {
        var errors = (exception.Trailers ?? [])
            .Where(entry => entry.Key == MetadataKey)
            .Select(entry => entry.Value)
            .ToList();
        if (errors.Count == 0)
        {
            return [FromGrpcStatusCode(exception.StatusCode, exception.Status.Detail, exception.Status.Detail)];
        }

        return errors
            .Select(entry => Split(entry) is { } parts
                ? FromGrpcStatusCode((StatusCode)parts.statusCode, parts.detail, parts.detail, parts.code)
                : Error.Unexpected("error.unknown", exception.Status.Detail))
            .ToList();
    }

    internal static StatusCode ToGrpcStatusCode(ErrorType type) => type switch
    {
        ErrorType.NotFound => StatusCode.NotFound,
        ErrorType.Conflict => StatusCode.AlreadyExists,
        ErrorType.Unauthorized => StatusCode.Unauthenticated,
        ErrorType.Forbidden => StatusCode.PermissionDenied,
        ErrorType.Unexpected => StatusCode.Internal,
        _ => StatusCode.InvalidArgument
    };

    private static ErrorType ToErrorType(StatusCode statusCode) => statusCode switch
    {
        StatusCode.NotFound => ErrorType.NotFound,
        StatusCode.AlreadyExists => ErrorType.Conflict,
        StatusCode.Unauthenticated => ErrorType.Unauthorized,
        StatusCode.PermissionDenied => ErrorType.Forbidden,
        StatusCode.Internal => ErrorType.Unexpected,
        _ => ErrorType.Validation
    };

    private static Error FromGrpcStatusCode(StatusCode statusCode, string description, string fallbackDetail, string? code = null) =>
        ToErrorType(statusCode) switch
        {
            ErrorType.NotFound => Error.NotFound(code ?? "error.not_found", description ?? fallbackDetail),
            ErrorType.Conflict => Error.Conflict(code ?? "error.conflict", description ?? fallbackDetail),
            ErrorType.Unauthorized => Error.Unauthorized(code ?? "error.unauthorized", description ?? fallbackDetail),
            ErrorType.Forbidden => Error.Forbidden(code ?? "error.forbidden", description ?? fallbackDetail),
            ErrorType.Unexpected => Error.Unexpected(code ?? "error.unexpected", description ?? fallbackDetail),
            _ => Error.Validation(code ?? "error.validation", description ?? fallbackDetail)
        };

    private static (int statusCode, string code, string detail)? Split(string entry)
    {
        var parts = entry.Split('|', 3);
        return parts.Length == 3 && int.TryParse(parts[0], out var statusCode)
            ? (statusCode, parts[1], parts[2])
            : null;
    }
}
