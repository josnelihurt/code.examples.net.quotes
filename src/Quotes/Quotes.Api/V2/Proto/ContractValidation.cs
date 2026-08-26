using System.Diagnostics;
using AspireQuotesPoc.ServiceDefaults.Errors;
using Quotes.Api.V2.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.V2.Proto;

/// <summary>
/// The v2 request messages arrive through JSON-PB, not model binding, so the contract-level
/// guards v0/v1 get from Data Annotations are mirrored here — field by field, message by
/// message — so a contract violation answers the byte-identical 400 problem on every
/// version. The keys and messages look like C# property names on purpose: they are what the
/// framework emits for the v0/v1 DTOs, and <c>VersionParityTests</c> holds all three to it.
/// </summary>
internal static class ContractValidation
{
    internal static HttpValidationProblemDetails? ValidateCreateQuote(CreateQuoteRequest request, HttpContext http)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrEmpty(request.Text))
        {
            errors["Text"] = ["The Text field is required."];
        }
        else if (request.Text.Length > QuoteRules.MaxTextLength)
        {
            errors["Text"] = [$"The field Text must be a string or array type with a maximum length of '{QuoteRules.MaxTextLength}'."];
        }

        if (string.IsNullOrEmpty(request.Author))
        {
            errors["Author"] = ["The Author field is required."];
        }
        else if (request.Author.Length > QuoteRules.MaxAuthorLength)
        {
            errors["Author"] = [$"The field Author must be a string or array type with a maximum length of '{QuoteRules.MaxAuthorLength}'."];
        }

        if (errors.Count == 0)
        {
            return null;
        }

        // Same construction path the framework takes for the annotated DTOs: validation
        // title, RFC 9110 type link, then the same extensions in the same insertion order
        // (traceId is what the framework pipeline stamps before the shared customize hook
        // adds errorCode and correlationId), so the serialized property order matches too.
        // The title and extension names duplicate ServiceDefaults internals deliberately —
        // VersionParityTests holds them to the v0/v1 bodies byte for byte.
        var problem = new HttpValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        problem.Extensions["errorCode"] = ProblemDetailsBuilder.RequestValidationErrorCode;
        problem.Extensions["correlationId"] = http.GetCorrelationId();

        return problem;
    }
}
