using System.Diagnostics;
using AspireQuotesPoc.ServiceDefaults.Errors;
using Microsoft.AspNetCore.Http;
using Quotes.Api.V2.Contracts;
using Quotes.Api.V2.Proto;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests.V2;

/// <summary>
/// The v2 request messages arrive through JSON-PB, so the Data Annotations guards v0/v1 get
/// for free are re-implemented by <see cref="ContractValidation"/> — field by field,
/// message by message. These tests pin the exact 400 problem shape that re-implementation
/// owes the parity suite: the framework's own keys, messages, title and extension insertion
/// order, because <c>VersionParityTests</c> compares the serialized bodies as parsed JSON.
/// </summary>
public class ContractValidationTests
{
    private const string _correlationHeaderName = "X-Correlation-Id";

    private static DefaultHttpContext NewHttpContext()
    {
        // A clean activity context so the traceId assertion below is deterministic:
        // ValidateCreateQuote prefers Activity.Current over the context's identifier.
        Activity.Current = null;
        var http = new DefaultHttpContext();
        http.Request.Headers[_correlationHeaderName] = "corr-test-value";
        http.TraceIdentifier = "trace-test-value";
        return http;
    }

    private static HttpValidationProblemDetails? Validate(string text, string author) =>
        ContractValidation.ValidateCreateQuote(new CreateQuoteRequest { Text = text, Author = author }, NewHttpContext());

    [Fact]
    public void A_valid_request_passes_untouched()
    {
        Validate("Talk is cheap. Show me the code.", "Linus Torvalds").ShouldBeNull();
    }

    [Theory]
    [InlineData("Text", "", "The Text field is required.", "Linus Torvalds")]
    [InlineData("Author", "", "The Author field is required.", "Talk is cheap. Show me the code.")]
    public void Required_violations_are_keyed_like_the_framework_s(string key, string value, string expectedMessage, string otherFieldValue)
    {
        var problem = (key switch
        {
            "Text" => Validate(value, otherFieldValue),
            _ => Validate(otherFieldValue, value)
        }).ShouldNotBeNull();

        problem.Errors[key].ShouldBe([expectedMessage]);
        problem.Errors.Keys.ShouldBe([key]);
    }

    [Fact]
    public void MaxLength_violations_quote_the_framework_s_messages()
    {
        // Over-length on both fields at once: each key gets its own framework message.
        var problem = Validate(
            new string('a', QuoteRules.MaxTextLength + 1),
            new string('b', QuoteRules.MaxAuthorLength + 1)).ShouldNotBeNull();

        problem.Errors["Text"].Single()
            .ShouldBe($"The field Text must be a string or array type with a maximum length of '{QuoteRules.MaxTextLength}'.");
        problem.Errors["Author"].Single()
            .ShouldBe($"The field Author must be a string or array type with a maximum length of '{QuoteRules.MaxAuthorLength}'.");
    }

    [Fact]
    public void Both_missing_fields_report_in_the_same_problem_in_declaration_order()
    {
        var problem = Validate("", "").ShouldNotBeNull();

        problem.Errors.Keys.ShouldBe(["Text", "Author"]);
    }

    [Fact]
    public void The_problem_shape_matches_the_framework_s_validation_problem()
    {
        var problem = Validate("", "").ShouldNotBeNull();

        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Title.ShouldBe("One or more validation errors occurred.");
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.1");

        // The framework pipeline stamps traceId before the shared customize hook adds
        // errorCode and correlationId; the serialized property order must match, and a
        // Dictionary enumerates in insertion order, so the key order pins it.
        problem.Extensions.Keys.ShouldBe(["traceId", "errorCode", "correlationId"]);
        problem.Extensions["traceId"].ShouldBe("trace-test-value");
        problem.Extensions["errorCode"].ShouldBe(ProblemDetailsBuilder.RequestValidationErrorCode);
        problem.Extensions["correlationId"].ShouldBe("corr-test-value");
    }
}
