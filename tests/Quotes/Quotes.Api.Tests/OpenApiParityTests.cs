using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quotes.Api.Tests;

/// <summary>
/// <see cref="VersionParityTests"/> proves the two transports answer identically on the wire;
/// this suite pins the contracts they publish. Descriptions, maxLength guards, required fields
/// and response-type metadata live only in the OpenAPI documents, so a description edited on one
/// version but not the other keeps every wire test green and only surfaces in the frozen YAML
/// artifacts — which are regenerated on demand, not on build. Both documents are produced by the
/// same host here, so anything not normalized by <see cref="NormalizeTransportLabels"/> is drift.
/// </summary>
[Collection(WebHostCollection.Name)]
public class OpenApiParityTests(QuoteApiFactory factory) : IClassFixture<QuoteApiFactory>
{
    private readonly QuoteApiFactory _factory = factory;

    [Fact]
    public async Task Both_versions_publish_the_same_contract()
    {
        using var client = _factory.CreateClient();

        var v0 = JsonNode.Parse(await client.GetStringAsync("/openapi/v0.json", TestContext.Current.CancellationToken));
        var v1 = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));

        NormalizeTransportLabels(v0!);
        NormalizeTransportLabels(v1!);

        Canonicalize(v0!).ShouldBe(Canonicalize(v1!));
    }

    /// <summary>
    /// Replaces the values that are transport labels rather than contract substance:
    /// <list type="bullet">
    /// <item>the version prefix in path keys and the document title suffix,</item>
    /// <item>operationIds and tags — route names must be version-local (Location headers stay
    /// inside their own version) and tags are Scalar grouping labels,</item>
    /// <item>the extra JSON negotiation content types MVC lists for request bodies. Both
    /// transports accept <c>application/json</c>, <c>text/json</c> and <c>application/*+json</c>
    /// on the wire; minimal APIs only document the first.</item>
    /// </list>
    /// Everything else — schemas, constraints, descriptions, parameters, response types and
    /// media types — must match.
    /// </summary>
    private static void NormalizeTransportLabels(JsonNode document)
    {
        var title = document["info"]!["title"]!.GetValue<string>();
        document["info"]!["title"] = title.Split('|')[0].Trim();

        document.AsObject().Remove("tags");

        var paths = document["paths"]!.AsObject();
        foreach (var (path, operations) in paths.ToArray())
        {
            paths.Remove(path);
            // "/api/v0/quotes/random" -> "/api/{version}/quotes/random"
            paths[$"/api/{{version}}/{path.Split('/', 4)[3]}"] = operations;

            foreach (var (_, operation) in operations!.AsObject())
            {
                var op = operation!.AsObject();
                op.Remove("operationId");
                op.Remove("tags");

                if (op["requestBody"]?["content"] is JsonObject content)
                {
                    content.Remove("text/json");
                    content.Remove("application/*+json");
                }
            }
        }
    }

    /// <summary>
    /// Serializes with object keys sorted so the comparison ignores ordering. The two transports
    /// legitimately order things differently (controllers list class-level response attributes
    /// before action-level ones), and JSON object key order carries no contract meaning.
    /// </summary>
    private static string Canonicalize(JsonNode document)
    {
        using var parsed = JsonDocument.Parse(document.ToJsonString());
        var builder = new StringBuilder();
        WriteCanonical(parsed.RootElement, builder);
        return builder.ToString();
    }

    private static void WriteCanonical(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    builder.Append(property.Name).Append(':');
                    WriteCanonical(property.Value, builder);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, builder);
                }
                builder.Append(']');
                break;
            default:
                builder.Append(element.GetRawText());
                break;
        }
    }
}
