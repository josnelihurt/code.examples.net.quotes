using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace AspireQuotesPoc.ServiceDefaults.Errors;

/// <summary>
/// Converts request-body binding failures (empty or malformed JSON on the minimal-API
/// path) into the shared validation envelope, so a garbage body answers the same
/// 400 <c>validation.request_invalid</c> problem on both transports instead of an
/// unhandled 500. Anything that is not a body-reading failure is left for the default
/// 500 path.
/// </summary>
public sealed class JsonBodyValidationExceptionHandler : IExceptionHandler
{
    private const string _problemContentType = "application/problem+json";

    // Same serialization defaults as the other middleware-produced problems (401 challenge,
    // 429 rejection); cached because this handler runs per failed request.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not (BadHttpRequestException or JsonException or InvalidDataException))
        {
            return false;
        }

        var problem = ProblemDetailsBuilder.Build(
            StatusCodes.Status400BadRequest,
            ProblemDetailsBuilder.RequestValidationErrorCode,
            "The request body could not be read as JSON.",
            httpContext);

        // Serialized by hand: WriteAsJsonAsync would stamp application/json over the
        // RFC 9457 content type every other problem body uses.
        httpContext.Response.StatusCode = problem.Status!.Value;
        httpContext.Response.ContentType = _problemContentType;
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem, _jsonOptions),
            cancellationToken);
        return true;
    }
}

