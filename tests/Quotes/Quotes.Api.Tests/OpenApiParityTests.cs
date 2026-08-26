using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quotes.Api.Tests;

/// <summary>
/// <see cref="VersionParityTests"/> proves the transports answer identically on the wire;
/// this suite pins the contracts they publish. Descriptions, maxLength guards, required fields
/// and response-type metadata live only in the OpenAPI documents, so a description edited on one
/// version but not the other keeps every wire test green and only surfaces in the frozen YAML
/// artifacts — which are regenerated on demand, not on build. Both documents are produced by the
/// same host here, so anything not normalized by <see cref="NormalizeTransportLabels"/> (and, for
/// the v1↔v2 pair, the schema-name labels in <see cref="NormalizeSchemaLabels"/>) is drift.
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

    [Fact]
    public async Task The_proto_contract_publishes_the_same_contract_as_the_handwritten_one()
    {
        using var client = _factory.CreateClient();

        var v1 = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var v2 = JsonNode.Parse(await client.GetStringAsync("/openapi/v2.json", TestContext.Current.CancellationToken));

        NormalizeTransportLabels(v1!);
        NormalizeTransportLabels(v2!);
        NormalizeSchemaLabels(v2!);
        ExpandSchemaRefs(v1!);
        ExpandSchemaRefs(v2!);

        Canonicalize(v1!).ShouldBe(Canonicalize(v2!));
    }

    /// <summary>
    /// v1 references the component schema for the list items; the proto pipeline inlines the
    /// element schema inside the page schema. A <c>$ref</c> to a component and an inline copy
    /// of that component are renderings of the same contract, so both documents are expanded
    /// here and the schema <i>content</i> is compared. Expansion is recursive with a cycle
    /// guard (a self-referencing schema would otherwise loop forever).
    /// </summary>
    private static void ExpandSchemaRefs(JsonNode document)
    {
        var schemas = document["components"]!["schemas"]!.AsObject();
        foreach (var (_, schema) in schemas)
        {
            if (schema is not null)
            {
                Expand(schema, schemas, []);
            }
        }

        Expand(document["paths"]!, schemas, []);

        return;

        static void Expand(JsonNode node, JsonObject schemas, HashSet<string> expanding)
        {
            if (node is JsonObject obj)
            {
                if (obj.TryGetPropertyValue("$ref", out var reference)
                    && reference is not null
                    && reference.GetValue<string>().StartsWith("#/components/schemas/", StringComparison.Ordinal))
                {
                    var name = reference.GetValue<string>().Split('/').Last();
                    if (schemas.TryGetPropertyValue(name, out var target)
                        && target is not null
                        && expanding.Add(name))
                    {
                        // Replace the ref node with a deep copy of the component so sibling
                        // keys (if any) are not silently dropped.
                        obj.Remove("$ref");
                        var copy = JsonNode.Parse(target.ToJsonString())!.AsObject();
                        foreach (var (key, value) in copy.ToArray())
                        {
                            copy.Remove(key); // detach so the node can be re-parented
                            obj[key] = value;
                        }

                        Expand(obj, schemas, expanding);
                        expanding.Remove(name);
                    }

                    return;
                }

                foreach (var (_, value) in obj.ToArray())
                {
                    if (value is not null)
                    {
                        Expand(value, schemas, expanding);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        Expand(item, schemas, expanding);
                    }
                }
            }
        }
    }

    /// <summary>
    /// v2's schemas are named after the proto messages (<c>Quote</c>, <c>CreateQuoteRequest</c>,
    /// <c>ListQuotesResponse</c>) while v0/v1 name the CLR DTOs they serialize. The names are
    /// transport labels — the wire fields the schemas describe are identical — so this helper
    /// renames the v2 component keys to their v1 counterparts and rewrites every
    /// <c>$ref</c> pointing at them. Everything else about the schemas must already match.
    /// </summary>
    private static void NormalizeSchemaLabels(JsonNode document)
    {
        var schemaNames = new Dictionary<string, string>
        {
            ["Quote"] = "QuoteResponseDto",
            ["CreateQuoteRequest"] = "CreateQuoteRequestDto",
            ["ListQuotesResponse"] = "QuotePageResponseDto"
        };

        var schemas = document["components"]!["schemas"]!.AsObject();
        foreach (var (name, renamed) in schemaNames)
        {
            if (schemas.ContainsKey(name))
            {
                var schema = schemas[name]!;
                schemas.Remove(name);
                schemas[renamed] = schema;
            }
        }

        RewriteRefs(document, schemaNames);
    }

    private static void RewriteRefs(JsonNode node, IReadOnlyDictionary<string, string> schemaNames)
    {
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj.ToArray())
            {
                if (key == "$ref" && value is not null)
                {
                    var reference = value.GetValue<string>();
                    var shortName = reference.Split('/').Last();
                    if (schemaNames.TryGetValue(shortName, out var renamed))
                    {
                        obj[key] = $"#/components/schemas/{renamed}";
                    }
                }
                else if (value is not null)
                {
                    RewriteRefs(value, schemaNames);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    RewriteRefs(item, schemaNames);
                }
            }
        }
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
