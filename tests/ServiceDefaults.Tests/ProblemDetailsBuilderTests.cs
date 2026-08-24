using System.Text.Json;
using AspireQuotesPoc.ServiceDefaults.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults.Tests;

public class ProblemDetailsBuilderTests
{
    [Fact]
    public void Build_carries_the_envelope_extensions_and_type_link()
    {
        var context = new DefaultHttpContext();
        context.Items[Extensions.CorrelationIdHeaderName] = "corr-42";

        var problem = ProblemDetailsBuilder.Build(
            StatusCodes.Status429TooManyRequests,
            "auth.rate_limited",
            "slow down",
            context);

        problem.Status.ShouldBe(StatusCodes.Status429TooManyRequests);
        problem.Title.ShouldBe("Too Many Requests");
        problem.Detail.ShouldBe("slow down");
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc9110#section-15.5.14");
        problem.Extensions["errorCode"].ShouldBe("auth.rate_limited");
        problem.Extensions["correlationId"].ShouldBe("corr-42");
    }

    [Fact]
    public void Build_without_a_context_omits_the_correlation_id()
    {
        var problem = ProblemDetailsBuilder.Build(
            StatusCodes.Status401Unauthorized,
            "auth.token_invalid",
            "nope",
            httpContext: null);

        problem.Extensions["errorCode"].ShouldBe("auth.token_invalid");
        problem.Extensions.ContainsKey("correlationId").ShouldBeFalse();
    }

    [Fact]
    public void Build_serializes_with_camel_case_extension_keys()
    {
        var problem = ProblemDetailsBuilder.Build(
            StatusCodes.Status401Unauthorized,
            "auth.token_missing",
            "missing",
            httpContext: null);

        var json = JsonSerializer.SerializeToNode(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json!["errorCode"].ShouldNotBeNull();
        json["type"].ShouldNotBeNull();
    }
}
