using System.Text.Json.Nodes;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Quotes.Api.V2.OpenApi;

/// <summary>
/// Builds OpenAPI schemas for the generated proto messages from their descriptors. Without
/// this transformer the generator would reflect the CLR surface of protobuf messages
/// (parsers, descriptors, plumbing), which is not the contract; the descriptor is. Structure
/// comes from <see cref="MessageDescriptor"/>; descriptions, examples and length limits come
/// from <see cref="ProtoContractDocs"/> so the v2 document reads like the v0/v1 ones.
/// </summary>
/// <remarks>
/// The shapes below deliberately emit what the v0/v1 documents emit, because
/// <c>OpenApiParityTests</c> holds this document to those byte for byte (modulo schema
/// names): integer properties carry <c>format</c>/<c>pattern</c> and the integer-or-string
/// type union the reflection-based generator produces, repeated message fields declare an
/// object-typed element schema the pipeline completes from the element's component (the v1
/// document renders the same thing as a <c>$ref</c> — a rendering difference the parity
/// suite normalizes away on both sides), and message-level descriptions are cleared — the
/// DTO transports do not lift their <c>[Description]</c> attributes onto component schemas,
/// so the proto pipeline's leading-comment descriptions must not either.
/// </remarks>
internal sealed class ProtoSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!typeof(IMessage).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        var descriptor = GetMessageDescriptor(context.JsonTypeInfo.Type);
        if (descriptor is null)
        {
            return Task.CompletedTask;
        }

        // See the class remarks: the DTO transports do not document their component schemas
        // at the type level, so the proto pipeline's message descriptions must not either.
        schema.Description = null;

        schema.Properties = descriptor.Fields.InFieldNumberOrder()
            .ToDictionary(
                field => field.JsonName,
                field => (IOpenApiSchema)ToSchema(descriptor.FullName, field));
        schema.Required = new HashSet<string>(descriptor.Fields.InFieldNumberOrder().Select(field => field.JsonName));

        if (ProtoContractDocs.Examples.TryGetValue(descriptor.FullName, out var example))
        {
            schema.Example = JsonNode.Parse(example);
        }

        return Task.CompletedTask;
    }

    private static MessageDescriptor? GetMessageDescriptor(Type type) =>
        Activator.CreateInstance(type) is IMessage instance ? instance.Descriptor : null;

    private static OpenApiSchema ToSchema(string messageFullName, FieldDescriptor field)
    {
        var schema = new OpenApiSchema
        {
            Type = field.IsRepeated ? JsonSchemaType.Array : ToJsonSchemaType(field.FieldType)
        };

        if (field.IsRepeated)
        {
            // Repeated message fields: the element schema below declares the message shape
            // (type object); the OpenAPI pipeline completes it from the element's component
            // schema, which is what the v0/v1 documents describe for their list items.
            schema.Items = field.MessageType is null
                ? new OpenApiSchema { Type = ToJsonSchemaType(field.FieldType) }
                : new OpenApiSchema { Type = JsonSchemaType.Object };
        }

        if (ProtoContractDocs.FieldDescriptions.TryGetValue($"{messageFullName}.{field.JsonName}", out var description))
        {
            schema.Description = description;
        }

        if (field.FieldType is FieldType.Int32 or FieldType.Int64)
        {
            // Same int shape the v0/v1 documents carry: proto3 JSON accepts int32/int64 as
            // number or string, and the reflection-based generator documents format and
            // pattern alongside that union.
            schema.Format = field.FieldType == FieldType.Int32 ? "int32" : "int64";
            schema.Pattern = field.FieldType == FieldType.Int32 ? "^-?(?:0|[1-9]\\d*)$" : "^-?[0-9]+$";
        }

        if (!field.IsRepeated
            && field.FieldType == FieldType.String
            && ProtoContractDocs.MaxLengths.TryGetValue($"{messageFullName}.{field.JsonName}", out var maxLength))
        {
            schema.MaxLength = maxLength;
        }

        return schema;
    }

    private static JsonSchemaType ToJsonSchemaType(FieldType fieldType) => fieldType switch
    {
        // The int shapes match the v0/v1 documents: proto3 JSON accepts int32 as number or
        // string, and the reflection-based generator documents exactly that union.
        FieldType.Int32 => JsonSchemaType.Integer | JsonSchemaType.String,
        FieldType.Int64 => JsonSchemaType.Integer | JsonSchemaType.String,
        FieldType.String => JsonSchemaType.String,
        _ => JsonSchemaType.String
    };
}
