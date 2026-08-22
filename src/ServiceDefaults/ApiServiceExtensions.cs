using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

public static class ApiServiceExtensions
{
    /// <summary>The document name <c>AddOpenApi()</c> produces by default.</summary>
    private const string _defaultDocumentName = "v1";

    /// <summary>
    /// Registers ProblemDetails and one OpenAPI document per name in <paramref name="documentNames"/>.
    /// Hosts that serve a single API version pass nothing and keep the framework default document
    /// name (<c>v1</c>, served at <c>/openapi/v1.json</c>); hosts that serve several versions name
    /// them explicitly, for example <c>AddStandardApiServices("v0", "v1")</c>.
    /// </summary>
    /// <remarks>
    /// An endpoint lands in a document when its ApiExplorer group name matches, so each version
    /// tags its own routes (<c>.WithGroupName(...)</c> for minimal APIs,
    /// <c>[ApiExplorerSettings(GroupName = ...)]</c> for controllers). Untagged endpoints appear in
    /// every document, which is what keeps single-version hosts working without any tagging.
    /// </remarks>
    public static TBuilder AddStandardApiServices<TBuilder>(this TBuilder builder, params string[] documentNames)
        where TBuilder : IHostApplicationBuilder
    {
        var names = documentNames.Length > 0 ? documentNames : [_defaultDocumentName];

        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton(new ApiDocumentNames(names));

        foreach (var documentName in names)
        {
            var name = documentName;
            builder.Services.AddOpenApi(name, options =>
            {
                options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
                options.ShouldInclude = description =>
                    description.GroupName is null || description.GroupName == name;
            });
        }

        return builder;
    }

    /// <summary>
    /// Serves every registered OpenAPI document and points Scalar at all of them, so a
    /// multi-version host gets a version picker in the reference UI.
    /// </summary>
    public static WebApplication MapStandardApiDocumentation(this WebApplication app)
    {
        var documentNames = app.Services.GetRequiredService<ApiDocumentNames>().Names;

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options
                .WithTitle($"{app.Environment.ApplicationName} API")
                .WithTheme(ScalarTheme.Purple)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithOpenApiRoutePattern("/openapi/{documentName}.json");

            foreach (var documentName in documentNames)
            {
                options.AddDocument(documentName);
            }
        });

        return app;
    }
}

/// <summary>Carries the OpenAPI document names from registration to endpoint mapping.</summary>
internal sealed record ApiDocumentNames(IReadOnlyList<string> Names);
