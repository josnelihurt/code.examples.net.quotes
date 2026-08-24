using System.Text.Json;
using System.Text.Json.Nodes;
using AspireQuotesPoc.ServiceDefaults.Errors;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Builds deterministic OpenAPI problem+json samples from colocated endpoint metadata.
/// </summary>
internal static class OpenApiProblemExampleBuilder
{
    /// <summary>Fixed sample value; real correlation ids are generated per request.</summary>
    internal const string SampleCorrelationId = "5c1f4a0e9d2b7386a4c0b1e8d3f69a27";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    internal static JsonObject? Build(OpenApiProblemExampleMetadata metadata)
    {
        if (!string.IsNullOrEmpty(metadata.ValidationProperty))
        {
            return BuildTransportValidation(metadata.ValidationProperty, metadata.ValidationMessage!);
        }

        if (metadata.StatusCode == StatusCodes.Status403Forbidden
            && string.IsNullOrEmpty(metadata.ErrorCode))
        {
            return Serialize(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = metadata.Title ?? "Forbidden",
                Detail = metadata.Detail,
                Type = ProblemDetailsBuilder.TypeLink(StatusCodes.Status403Forbidden)
            });
        }

        if (metadata.StatusCode == StatusCodes.Status429TooManyRequests
            && !string.IsNullOrEmpty(metadata.ErrorCode))
        {
            return Serialize(BuildRateLimitProblem(metadata));
        }

        var error = metadata.Error ?? SynthesizeError(metadata);
        if (error is not { } resolved)
        {
            return null;
        }

        return Serialize(ProblemDetailsFactory.Create([resolved], CreateSampleContext()));
    }

    private static JsonObject BuildTransportValidation(string propertyName, string message) =>
        new()
        {
            ["type"] = ProblemDetailsBuilder.TypeLink(StatusCodes.Status400BadRequest),
            ["title"] = ProblemDetailsFactory.ValidationTitle,
            ["status"] = StatusCodes.Status400BadRequest,
            ["errorCode"] = ProblemDetailsBuilder.RequestValidationErrorCode,
            ["correlationId"] = SampleCorrelationId,
            ["errors"] = new JsonObject
            {
                [propertyName] = new JsonArray(message),
            },
        };

    private static ProblemDetails BuildRateLimitProblem(OpenApiProblemExampleMetadata metadata)
    {
        var statusCode = StatusCodes.Status429TooManyRequests;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = metadata.Detail,
            Type = ProblemDetailsBuilder.TypeLink(statusCode)
        };

        problem.Extensions[ProblemDetailsFactory.CorrelationIdExtension] = SampleCorrelationId;
        problem.Extensions[ProblemDetailsFactory.ErrorCodeExtension] = metadata.ErrorCode!;
        return problem;
    }

    private static Error? SynthesizeError(OpenApiProblemExampleMetadata metadata)
    {
        if (string.IsNullOrEmpty(metadata.ErrorCode) || string.IsNullOrEmpty(metadata.Detail))
        {
            return null;
        }

        return metadata.StatusCode switch
        {
            StatusCodes.Status400BadRequest => Error.Validation(metadata.ErrorCode, metadata.Detail),
            StatusCodes.Status401Unauthorized => Error.Unauthorized(metadata.ErrorCode, metadata.Detail),
            StatusCodes.Status404NotFound => Error.NotFound(metadata.ErrorCode, metadata.Detail),
            StatusCodes.Status409Conflict => Error.Conflict(metadata.ErrorCode, metadata.Detail),
            _ => Error.Validation(metadata.ErrorCode, metadata.Detail)
        };
    }

    private static JsonObject Serialize(ProblemDetails problem)
    {
        var node = JsonSerializer.SerializeToNode(problem, _jsonOptions) as JsonObject
            ?? throw new InvalidOperationException("ProblemDetails did not serialize to a JSON object.");

        if (problem is HttpValidationProblemDetails validation && validation.Errors.Count > 0)
        {
            var errors = new JsonObject();
            foreach (var (key, messages) in validation.Errors)
            {
                errors[key] = new JsonArray(messages.Select(static message => JsonValue.Create(message)).ToArray());
            }

            node["errors"] = errors;
        }

        return node;
    }

    private static HttpContext CreateSampleContext()
    {
        var context = new DefaultHttpContext();
        context.Items[Extensions.CorrelationIdHeaderName] = SampleCorrelationId;
        return context;
    }
}
