using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Attaches colocated problem+json samples declared via <see cref="OpenApiProblemExampleAttribute"/>
/// or <see cref="OpenApiRouteHandlerExtensions"/> to matching error responses.
/// </summary>
internal sealed class OpenApiProblemExampleTransformer : IOpenApiOperationTransformer
{
    private const string _problemContentType = "application/problem+json";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var examples = CollectExamples(context);

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

            var metadata = examples.FirstOrDefault(example => example.StatusCode == status);
            if (metadata is null)
            {
                continue;
            }

            mediaType.Example ??= OpenApiProblemExampleBuilder.Build(metadata);
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<OpenApiProblemExampleMetadata> CollectExamples(
        OpenApiOperationTransformerContext context)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var examples = new List<OpenApiProblemExampleMetadata>();

        foreach (var item in metadata)
        {
            switch (item)
            {
                case OpenApiProblemExampleMetadata exampleMetadata:
                    examples.Add(exampleMetadata);
                    break;
                case OpenApiProblemExampleAttribute attribute:
                    examples.Add(attribute.ToMetadata());
                    break;
            }
        }

        return examples;
    }
}
