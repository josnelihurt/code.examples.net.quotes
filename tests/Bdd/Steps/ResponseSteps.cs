using System.Net;
using System.Text.Json;
using AspireQuotesPoc.Specs.Support;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Steps;

/// <summary>
/// Shared Then steps: every scenario asserts through this vocabulary, so a status code or
/// problem shape is described exactly once.
/// </summary>
[Binding]
public sealed class ResponseSteps(ApiWorld world)
{
    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int expected)
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.StatusCode.ShouldBe((HttpStatusCode)expected, $"body was {world.LastBody}");
    }

    [Then("the response is a problem document")]
    public void ThenTheResponseIsAProblemDocument()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        world.LastBody.ShouldNotBeNull("problem bodies are JSON");

        // The domain error envelope shared by both APIs: RFC 9457 plus errorCode/correlationId.
        world.LastBody.Value.TryGetProperty("errorCode", out _).ShouldBeTrue(
            $"domain problems carry errorCode; body was {world.LastBody}");
    }

    [Then("the response is a validation problem")]
    public void ThenTheResponseIsAValidationProblem()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        world.LastBody.ShouldNotBeNull("problem bodies are JSON");

        // Transport validation (Data Annotations) produces the other documented 400 shape:
        // field-keyed errors instead of a single errorCode.
        world.LastBody.Value.TryGetProperty("errors", out var errors).ShouldBeTrue(
            $"validation problems carry field-keyed errors; body was {world.LastBody}");
        errors.EnumerateObject().ShouldNotBeEmpty("at least one field must be flagged");
    }

    [Then("the problem errorCode is {string}")]
    public void ThenTheProblemErrorCodeIs(string expected)
    {
        var body = world.LastBody.ShouldNotBeNull("a problem response must be recorded first");
        body.GetProperty("errorCode").GetString().ShouldBe(expected, $"body was {body}");
    }

    [Then("the response body has {string}")]
    public void ThenTheResponseBodyHas(string property) => HasProperties(property);

    [Then("the response body has {string} and {string}")]
    public void ThenTheResponseBodyHas(string first, string second) => HasProperties(first, second);

    [Then("the X-Correlation-Id header is echoed")]
    public void ThenTheCorrelationIdHeaderIsEchoed()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(world.CorrelationId);
    }

    [Then("the response carries a Location header")]
    public void ThenTheResponseCarriesALocationHeader()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        world.LastCreatedLocation.ShouldNotBeNullOrWhiteSpace("201 responses must address the new quote");
    }

    [Then("fetching that location returns the quote I published")]
    public async Task ThenFetchingThatLocationReturnsTheQuoteIPublished()
    {
        world.LastCreatedLocation.ShouldNotBeNullOrWhiteSpace("a Location header must be recorded first");
        var location = world.LastCreatedLocation.ShouldNotBeNull();

        // CreatedAtRoute emits an absolute URL naming the host the API saw; fetch just the
        // path through the same gateway client regardless (test-api.sh did the same dance).
        var path = location.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(location).PathAndQuery
            : location;
        var response = await world.Client.GetAsync(path);
        await world.RecordAsync(response);

        ThenTheResponseStatusIs(200);
        ThenTheResponseBodyIsTheQuoteIPublished();
    }

    [Then("the response body is the quote I published")]
    public void ThenTheResponseBodyIsTheQuoteIPublished()
    {
        var body = world.LastBody.ShouldNotBeNull("a JSON response must be recorded first");
        body.GetProperty("text").GetString().ShouldBe(world.UniqueText);
        body.GetProperty("author").GetString().ShouldBe("Specification Suite");
    }

    [Then("the response reports page 1 with the default page size")]
    public void ThenTheResponseReportsPage1WithTheDefaultPageSize()
    {
        var body = world.LastBody.ShouldNotBeNull("a JSON response must be recorded first");
        body.GetProperty("page").GetInt32().ShouldBe(1);
        body.GetProperty("pageSize").GetInt32().ShouldBe(20);
        body.GetProperty("items").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Then("the response carries a WWW-Authenticate header")]
    public void ThenTheResponseCarriesAWwwAuthenticateHeader()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.Headers.WwwAuthenticate.ShouldNotBeEmpty("401 responses must state the challenge");
    }

    [Then("the introspection says the token is valid for {string}")]
    public void ThenTheIntrospectionSaysTheTokenIsValidFor(string username)
    {
        var body = world.LastBody.ShouldNotBeNull("a JSON response must be recorded first");
        body.GetProperty("valid").GetBoolean().ShouldBeTrue();
        body.GetProperty("username").GetString().ShouldBe(username);
    }

    [Then("the introspection says the token is invalid")]
    public void ThenTheIntrospectionSaysTheTokenIsInvalid()
    {
        var body = world.LastBody.ShouldNotBeNull("a JSON response must be recorded first");
        body.GetProperty("valid").GetBoolean().ShouldBeFalse();
    }

    [Then("the response carries no Location header")]
    public void ThenTheResponseCarriesNoLocationHeader()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.Headers.Location.ShouldBeNull("transcoded create answers 200 without addressing the quote");
    }

    /// <summary>
    /// The v3 drift vocabulary: stock gRPC-JSON transcoding answers failures with the gRPC
    /// status envelope (<c>{"code","message","details"}</c>) instead of RFC 9457
    /// problem+json. These steps exist so TranscodedQuotes.feature can pin that envelope
    /// without weakening the problem-document assertions above.
    /// </summary>
    [Then("the response is a grpc status envelope")]
    public void ThenTheResponseIsAGrpcStatusEnvelope()
    {
        var response = world.LastResponse.ShouldNotBeNull("a request step must run first");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var body = world.LastBody.ShouldNotBeNull("gRPC status bodies are JSON");
        body.GetProperty("code").ValueKind.ShouldBe(JsonValueKind.Number, $"body was {body}");
        body.GetProperty("message").ValueKind.ShouldBe(JsonValueKind.String, $"body was {body}");
        body.GetProperty("details").ValueKind.ShouldBe(JsonValueKind.Array, $"body was {body}");
    }

    [Then("the grpc status code is {int}")]
    public void ThenTheGrpcStatusCodeIs(int expected)
    {
        var body = world.LastBody.ShouldNotBeNull("a gRPC status response must be recorded first");
        body.GetProperty("code").GetInt32().ShouldBe(expected, $"body was {body}");
    }

    [Then("the grpc message mentions {string}")]
    public void ThenTheGrpcMessageMentions(string fragment)
    {
        var body = world.LastBody.ShouldNotBeNull("a gRPC status response must be recorded first");
        var message = body.GetProperty("message").GetString().ShouldNotBeNull($"body was {body}");
        message.Contains(fragment, StringComparison.Ordinal).ShouldBeTrue($"message was {message}");
    }

    private void HasProperties(params string[] properties)
    {
        var body = world.LastBody.ShouldNotBeNull("a JSON response must be recorded first");
        foreach (var property in properties)
        {
            body.TryGetProperty(property, out _).ShouldBeTrue($"body should carry '{property}'; was {body}");
        }
    }
}
