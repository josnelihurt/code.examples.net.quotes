using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Applies the host's <see cref="OpenApiDocumentInfo"/> to the generated document: the info
/// description (the narrative consumers read first in Scalar) and the tag descriptions shown
/// next to each operation group.
/// </summary>
internal sealed class DocumentInfoTransformer(IServiceProvider serviceProvider) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var info = serviceProvider.GetService<OpenApiDocumentInfo>();
        if (info is null)
        {
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(info.Description))
        {
            document.Info ??= new OpenApiInfo();
            document.Info.Description = info.Description;
        }

        if (info.TagDescriptions is not { Count: > 0 })
        {
            return Task.CompletedTask;
        }

        // The generator only lists tags it found on endpoints; entries may be missing until
        // every documented tag is actually used, so fill in or update by name.
        document.Tags ??= new HashSet<OpenApiTag>();
        foreach (var (name, description) in info.TagDescriptions)
        {
            var tag = document.Tags.FirstOrDefault(tag => tag.Name == name);
            if (tag is null)
            {
                document.Tags.Add(new OpenApiTag { Name = name, Description = description });
            }
            else
            {
                tag.Description ??= description;
            }
        }

        return Task.CompletedTask;
    }
}
