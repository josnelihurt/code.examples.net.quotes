using AspireQuotesPoc.ServiceDefaults.Errors;
using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

public static class ApiServiceExtensions
{
    /// <summary>The document name <c>AddOpenApi()</c> produces by default.</summary>
    private const string _defaultDocumentName = "v1";

    /// <summary>
    /// Registers ProblemDetails and the document names Scalar offers in its version picker.
    /// Hosts that serve a single API version pass nothing and keep the framework default
    /// document name (<c>v1</c>, served at <c>/openapi/v1.json</c>); hosts that serve several
    /// versions name them explicitly, for example <c>AddStandardApiServices("v0", "v1")</c>.
    /// </summary>
    /// <remarks>
    /// This method deliberately does not call <c>AddOpenApi</c>: the XML-comment source
    /// generator only intercepts <c>AddOpenApi</c> calls whose document name is a string
    /// literal, so each host registers its own documents next to this call, for example
    /// <c>builder.Services.AddOpenApi("v1", o => o.ConfigureStandardOpenApi("v1"))</c>.
    /// </remarks>
    public static TBuilder AddStandardApiServices<TBuilder>(this TBuilder builder, params string[] documentNames)
        where TBuilder : IHostApplicationBuilder
    {
        var names = documentNames.Length > 0 ? documentNames : [_defaultDocumentName];

        builder.Services.AddProblemDetails(options =>
        {
            // Framework-produced transport validation (Data Annotations via AddValidation,
            // model binding) keys `errors` by property name and carries no errorCode. Stamp
            // the envelope here so every 400 a client can meet is shape-identical across the
            // minimal-API and MVC pipelines. ErrorOr-driven problems already carry their own
            // errorCode and are left untouched.
            options.CustomizeProblemDetails = context =>
            {
                if (context.ProblemDetails is HttpValidationProblemDetails validation
                    && !validation.Extensions.ContainsKey(ProblemDetailsFactory.ErrorCodeExtension))
                {
                    validation.Extensions[ProblemDetailsFactory.ErrorCodeExtension] =
                        ProblemDetailsBuilder.RequestValidationErrorCode;
                    validation.Extensions[ProblemDetailsFactory.CorrelationIdExtension] =
                        context.HttpContext.GetCorrelationId();
                }
            };
        });

        // Hosts call the parameterless UseExceptionHandler(); this handler makes unreadable
        // JSON bodies answer the shared 400 envelope on both transports instead of a 500.
        builder.Services.AddExceptionHandler<JsonBodyValidationExceptionHandler>();

        builder.Services.AddSingleton(new ApiDocumentNames(names));

        return builder;
    }

    /// <summary>
    /// Applies the standard document wiring to one OpenAPI document: the bearer security
    /// scheme, the problem+json response samples, the host narrative
    /// (<see cref="OpenApiDocumentInfo"/>) and the per-version endpoint filter. The
    /// <paramref name="documentName"/> must equal the literal passed to
    /// <c>AddOpenApi</c> at the call site.
    /// </summary>
    public static OpenApiOptions ConfigureStandardOpenApi(this OpenApiOptions options, string documentName)
    {
        options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<OpenApiProblemExampleTransformer>();
        options.AddDocumentTransformer<DocumentInfoTransformer>();
        options.ShouldInclude = description =>
            description.GroupName is null || description.GroupName == documentName;
        return options;
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
