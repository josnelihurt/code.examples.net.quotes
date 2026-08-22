using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Quotes.Api;
using Quotes.Api.Telemetry;
using Quotes.Api.V0.Controllers;
using Quotes.Api.V1.Endpoints;
using Quotes.Application;
using Quotes.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    // Two transports, two OpenAPI documents: v0 is controller-based, v1 is minimal APIs.
    builder.AddStandardApiServices(QuotesController.DocumentName, QuoteEndpoints.DocumentName);
    builder.Services.AddSingleton(new OpenApiDocumentInfo(
        Description: OpenApiDocs.Description,
        TagDescriptions: OpenApiDocs.TagDescriptions));
    // Literal document names are mandatory: the XML-comment source generator only intercepts
    // AddOpenApi calls whose document name is a string literal, so a loop or a constant field
    // would silently drop every /// summary and response description from the documents.
    builder.Services.AddOpenApi("v0", options => options.ConfigureStandardOpenApi("v0"));
    builder.Services.AddOpenApi("v1", options => options.ConfigureStandardOpenApi("v1"));
    builder.AddStandardJwtAuthentication();

    // The API host is the composition root: each layer contributes its own registrations.
    builder.Services.AddQuotesApplication();
    builder.Services.AddQuotesInfrastructure();
    builder.Services.AddQuotesUseCaseTelemetry();
    builder.Services.AddValidation();
    builder.Services.AddStandardControllers();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogDefaults();
    app.UseCorrelationId();
    app.UseStandardAuthentication();
    app.MapDefaultEndpoints();
    app.MapStandardApiDocumentation();

    // Both transports resolve the same decorated use cases from the same container.
    QuoteEndpoints.Map(app);
    app.MapControllers();

    await app.RunAsync();
}
// S2139: the log-and-rethrow is deliberate. Serilog is flushed in the finally block, so the
// fatal entry has to be written before the exception leaves the host.
#pragma warning disable S2139
catch (Exception ex)
{
    Log.Fatal(ex, "Quotes.Api terminated unexpectedly");
    throw;
}
#pragma warning restore S2139
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry-point marker for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
