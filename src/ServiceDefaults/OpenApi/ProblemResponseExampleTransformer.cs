using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Attaches a sample problem+json body to every declared error response so consumers can see
/// the shared error envelope (RFC 9457 with <c>errorCode</c> and <c>correlationId</c>
/// extensions) without making a failing call. Samples are selected from the version-stripped
/// route — the only path label that is identical across the two Quotes transports — so the
/// v0/v1 documents always receive the same bodies and the parity suite stays green.
/// </summary>
internal sealed partial class ProblemResponseExampleTransformer : IOpenApiOperationTransformer
{
    private const string _problemContentType = "application/problem+json";

    /// <summary>Fixed sample value; real correlation ids are generated per request.</summary>
    private const string _sampleCorrelationId = "5c1f4a0e9d2b7386a4c0b1e8d3f69a27";

    private const string _type400 = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string _type401 = "https://tools.ietf.org/html/rfc9110#section-15.5.2";
    private const string _type404 = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string _type409 = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private const string _rateLimitDetail = "The auth endpoint rate limit was exceeded; retry after the window elapses.";
    private const string _challengeDetail = "A valid bearer token is required.";
    private const string _forbiddenDetail = "The access token is missing the required scope (quotes:read or quotes:write).";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var (statusCode, response) in operation.Responses ?? new OpenApiResponses())
        {
            if (!int.TryParse(statusCode, CultureInfo.InvariantCulture, out var status) || status < 400)
            {
                continue;
            }

            var content = response.Content;
            if (content is null || !content.TryGetValue(_problemContentType, out var mediaType))
            {
                continue;
            }

            // Never overwrite a sample that came from the operation's own metadata.
            mediaType.Example ??= BuildExample(status, context);
        }

        return Task.CompletedTask;
    }

    private static JsonObject? BuildExample(int status, OpenApiOperationTransformerContext context)
    {
        var route = NormalizeRoute(context.Description.RelativePath);
        var method = context.Description.HttpMethod;

        if (method == "POST" && route == "auth/login")
        {
            return LoginExample(status);
        }

        if (method == "POST" && route == "auth/validate")
        {
            return ValidateExample(status);
        }

        if (route.StartsWith("quotes", StringComparison.Ordinal))
        {
            return method == "POST" ? CreateExample(status) : ReadExample(status);
        }

        return null;
    }

    private static JsonObject? LoginExample(int status) => status switch
    {
        // Framework transport validation: errors are keyed by property name and the body
        // carries no errorCode (the correlation id travels in the X-Correlation-Id header).
        400 => new JsonObject
        {
            ["type"] = _type400,
            ["title"] = "One or more validation errors occurred.",
            ["status"] = 400,
            ["errors"] = new JsonObject
            {
                ["Username"] = new JsonArray("The Username field is required."),
            },
        },
        401 => Problem(401, "Unauthorized", "Invalid credentials.", _type401, "auth.invalid_credentials"),
        429 => Problem(429, "Too many requests", _rateLimitDetail, errorCode: "auth.rate_limited"),
        _ => null,
    };

    private static JsonObject? ValidateExample(int status) => status switch
    {
        // Missing-token failures go through the ErrorOr pipeline: a validation problem whose
        // errors are keyed by error code, not by property name.
        400 => ValidationProblem("auth.token_missing", "An access token is required."),
        429 => Problem(429, "Too many requests", _rateLimitDetail, errorCode: "auth.rate_limited"),
        _ => null,
    };

    private static JsonObject? ReadExample(int status) => status switch
    {
        400 => ValidationProblem("quote.invalid_page_request", "The requested page or page size is outside the allowed range."),
        401 => Problem(401, "Unauthorized", _challengeDetail, errorCode: "auth.token_invalid"),
        403 => Problem(403, "Forbidden", _forbiddenDetail),
        404 => Problem(404, "Not Found", "Quote not found.", _type404, "quote.not_found"),
        _ => null,
    };

    private static JsonObject? CreateExample(int status) => status switch
    {
        400 => ValidationProblem("quote.text_too_short", "Quote text must be at least 12 characters."),
        401 => Problem(401, "Unauthorized", _challengeDetail, errorCode: "auth.token_invalid"),
        403 => Problem(403, "Forbidden", _forbiddenDetail),
        409 => Problem(409, "Conflict", "A quote with the same meaning already exists.", _type409, "quote.duplicate_fingerprint"),
        _ => null,
    };

    private static JsonObject Problem(
        int status,
        string title,
        string? detail = null,
        string? type = null,
        string? errorCode = null)
    {
        var problem = new JsonObject();
        if (type is not null)
        {
            problem["type"] = type;
        }

        problem["title"] = title;
        problem["status"] = status;
        if (detail is not null)
        {
            problem["detail"] = detail;
        }

        if (errorCode is not null)
        {
            problem["errorCode"] = errorCode;
            problem["correlationId"] = _sampleCorrelationId;
        }

        return problem;
    }

    private static JsonObject ValidationProblem(string errorCode, string message)
    {
        var problem = Problem(400, "One or more validation errors occurred.", type: _type400, errorCode: errorCode);
        problem["errors"] = new JsonObject
        {
            [errorCode] = new JsonArray(message),
        };
        return problem;
    }

    /// <summary>Strips the version segment so both transports of an operation share one key.</summary>
    [GeneratedRegex(@"^api/v\d+/")]
    private static partial Regex VersionSegment();

    private static string NormalizeRoute(string? relativePath) =>
        relativePath is null ? string.Empty : VersionSegment().Replace(relativePath.TrimStart('/'), string.Empty);
}
