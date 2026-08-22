using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AspireQuotesPoc.ServiceDefaults.OpenApi;

/// <summary>
/// Adds the Bearer security scheme to the document and marks every endpoint that requires
/// authorization as secured, so generated clients wire up authentication correctly.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(IServiceProvider serviceProvider) : IOpenApiOperationTransformer
{
    private const string _schemeName = "Bearer";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Resolved lazily: hosts without authentication configured have no scheme provider,
        // and the transformer must not break document generation for them.
        var schemeProvider = serviceProvider.GetService<IAuthenticationSchemeProvider>();
        if (schemeProvider is null)
        {
            return Task.CompletedTask;
        }

        return TransformAsync(operation, context, schemeProvider);
    }

    private static async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        IAuthenticationSchemeProvider schemeProvider)
    {
        var schemes = await schemeProvider.GetAllSchemesAsync();
        if (schemes.All(scheme => scheme.Name != _schemeName))
        {
            return;
        }

        var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any();
        if (!requiresAuthorization)
        {
            return;
        }

        var document = context.Document;
        if (document is null)
        {
            return;
        }

        var components = document.Components ?? new OpenApiComponents();
        document.Components = components;
        var securitySchemes = components.SecuritySchemes ?? new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes = securitySchemes;
        securitySchemes.TryAdd(_schemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter the bearer token issued by the Auth API."
        });

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                // The host document anchor is required: without it the requirement
                // serializes as an empty entry (`security: - {}`), which consumers read
                // as "authentication optional" instead of "bearer required".
                [new OpenApiSecuritySchemeReference(_schemeName, document)] = []
            }
        ];
    }
}
