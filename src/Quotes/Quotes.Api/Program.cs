using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Microsoft.EntityFrameworkCore;
using Quotes.Api;
using Quotes.Api.Telemetry;
using Quotes.Api.V0.Controllers;
using Quotes.Api.V1.Endpoints;
using Quotes.Application;
using Quotes.Infrastructure;
using Quotes.Infrastructure.Persistence;
using Serilog;

// The v2/v3 namespaces are reached through aliases: the top-level Program cannot see
// sibling sub-namespace names through the root using above, and a plain using would make
// QuoteEndpoints ambiguous against the v1 one.
using V2 = Quotes.Api.V2;
using V3 = Quotes.Api.V3;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    // Four transports, three OpenAPI documents: v0 is controller-based, v1 is minimal APIs,
    // v2 binds the proto contract through an adapter. v3 is served by stock gRPC-JSON
    // transcoding, which ApiExplorer cannot see — the proto file is its contract of record,
    // so there is no v3 document on purpose.
    builder.AddStandardApiServices(
        QuotesController.DocumentName,
        QuoteEndpoints.DocumentName,
        V2.Endpoints.QuoteEndpoints.DocumentName);
    builder.Services.AddSingleton<IReadOnlyDictionary<string, OpenApiDocumentInfo>>(OpenApiDocs.Documents);
    // Literal document names are mandatory: the XML-comment source generator only intercepts
    // AddOpenApi calls whose document name is a string literal, so a loop or a constant field
    // would silently drop every /// summary and response description from the documents.
    builder.Services.AddOpenApi("v0", options => options.ConfigureStandardOpenApi("v0"));
    builder.Services.AddOpenApi("v1", options => options.ConfigureStandardOpenApi("v1"));
    // The v2 document's schemas come from the proto descriptors, not CLR reflection.
    builder.Services.AddOpenApi("v2", options =>
    {
        options.ConfigureStandardOpenApi("v2");
        options.AddSchemaTransformer<V2.OpenApi.ProtoSchemaTransformer>();
    });
    // v3: the platform runtime serves the annotated proto directly over JSON.
    builder.Services.AddGrpc().AddJsonTranscoding();
    builder.AddStandardJwtAuthentication(
        (QuoteScopes.ReadPolicy, QuoteScopes.ReadScope),
        (QuoteScopes.WritePolicy, QuoteScopes.WriteScope));

    // The API host is the composition root: each layer contributes its own registrations.
    builder.Services.AddQuotesApplication();
    builder.AddQuotesInfrastructure();
    builder.Services.AddQuotesUseCaseTelemetry();
    builder.Services.AddValidation();
    builder.Services.AddStandardControllers();
    // The v2 adapter invokes the generated service in-process; both resolve the same
    // decorated use cases from the same container as v0/v1. Scoped, because the use
    // cases are scoped and the adapter resolves it per request.
    builder.Services.AddScoped<V2.Services.QuoteGrpcService>();

    var app = builder.Build();

    // The catalog database is created/migrated before serving, under Aspire and in a
    // standalone boot alike. MigrateAsync is idempotent and EF Core 9+ takes a
    // database-wide migration lock, so replicas starting together cannot corrupt it.
    await using (var scope = app.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider
            .GetRequiredService<QuotesDbContext>()
            .Database.MigrateAsync();
    }

    app.UseExceptionHandler();
    app.UseSerilogDefaults();
    app.UseCorrelationId();
    app.UseStandardAuthentication();
    app.MapDefaultEndpoints();
    app.MapStandardApiDocumentation();

    // All four transports resolve the same decorated use cases from the same container.
    QuoteEndpoints.Map(app);
    V2.Endpoints.QuoteEndpoints.Map(app);
    app.MapGrpcService<V3.Services.QuoteGrpcService>();
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
