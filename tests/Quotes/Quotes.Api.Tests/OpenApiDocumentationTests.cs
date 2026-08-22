using System.Text.Json.Nodes;

namespace Quotes.Api.Tests;

/// <summary>
/// Guards the OpenAPI documentation itself: operation summaries and descriptions, parameter
/// docs with examples, schema examples and problem+json samples must exist on both versions.
/// The XML-comment source generator only activates for <c>AddOpenApi</c> calls with a literal
/// document name, so this suite doubles as the tripwire for that wiring: a refactor that goes
/// back to a looped name silently empties the documents while every wire test stays green.
/// </summary>
[Collection(WebHostCollection.Name)]
public class OpenApiDocumentationTests(QuoteApiFactory factory) : IClassFixture<QuoteApiFactory>
{
    private readonly QuoteApiFactory _factory = factory;

    [Theory]
    [InlineData("v0")]
    [InlineData("v1")]
    public async Task Every_quote_operation_is_fully_documented(string documentName)
    {
        var document = await FetchDocumentAsync(documentName);

        document["info"]!["description"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();

        foreach (var (path, operations) in document["paths"]!.AsObject())
        {
            if (!path.StartsWith("/api/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var (_, operation) in operations!.AsObject())
            {
                var op = operation!;
                op["summary"].ShouldNotBeNull();
                op["description"].ShouldNotBeNull();

                foreach (var (_, response) in op["responses"]!.AsObject())
                {
                    response!["description"].ShouldNotBeNull();
                }
            }
        }
    }

    [Theory]
    [InlineData("v0")]
    [InlineData("v1")]
    public async Task Pagination_parameters_carry_descriptions_and_examples(string documentName)
    {
        var document = await FetchDocumentAsync(documentName);
        var list = document["paths"]![$"/api/{documentName}/quotes"]!["get"]!;

        var parameters = list["parameters"]!.AsArray();
        parameters.ShouldContain(parameter => parameter!["name"]!.GetValue<string>() == "page");
        parameters.ShouldContain(parameter => parameter!["name"]!.GetValue<string>() == "pageSize");

        foreach (var parameter in parameters)
        {
            parameter!["description"].ShouldNotBeNull();
            parameter["example"].ShouldNotBeNull();
        }
    }

    [Theory]
    [InlineData("v0")]
    [InlineData("v1")]
    public async Task Request_bodies_carry_the_body_param_description(string documentName)
    {
        // The XML-comment generator maps the LAST <param> tag to the request body, so the
        // body parameter must be documented last. This pins that the mapping stays correct.
        var document = await FetchDocumentAsync(documentName);
        var create = document["paths"]![$"/api/{documentName}/quotes"]!["post"]!;

        create["requestBody"]!["description"]!.GetValue<string>()
            .ShouldContain("quote text and its author");
    }

    [Theory]
    [InlineData("v0")]
    [InlineData("v1")]
    public async Task Schemas_carry_examples_and_errors_carry_samples(string documentName)
    {
        var document = await FetchDocumentAsync(documentName);

        var schemas = document["components"]!["schemas"]!.AsObject();
        schemas["CreateQuoteRequestDto"]!["example"].ShouldNotBeNull();
        schemas["QuoteResponseDto"]!["example"].ShouldNotBeNull();
        schemas["QuotePageResponseDto"]!["example"].ShouldNotBeNull();

        var create = document["paths"]![$"/api/{documentName}/quotes"]!["post"]!;
        create["responses"]!["409"]!["description"]!.GetValue<string>()
            .ShouldContain("quote.duplicate_fingerprint");
        ProblemExample(create, "409")!["errorCode"]!.GetValue<string>()
            .ShouldBe("quote.duplicate_fingerprint");

        var list = document["paths"]![$"/api/{documentName}/quotes"]!["get"]!;
        list["responses"]!["400"]!["description"]!.GetValue<string>()
            .ShouldContain("quote.invalid_page_request");
        ProblemExample(list, "400")!["errorCode"]!.GetValue<string>()
            .ShouldBe("quote.invalid_page_request");
    }

    private static JsonNode? ProblemExample(JsonNode operation, string statusCode) =>
        operation["responses"]![statusCode]!["content"]!["application/problem+json"]!["example"];

    private async Task<JsonNode> FetchDocumentAsync(string documentName)
    {
        using var client = _factory.CreateClient();
        var json = await client.GetStringAsync(
            $"/openapi/{documentName}.json",
            TestContext.Current.CancellationToken);
        return JsonNode.Parse(json)!;
    }
}
