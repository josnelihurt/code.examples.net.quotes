using AspireQuotesPoc.ServiceDefaults.OpenApi;
using Microsoft.EntityFrameworkCore;
using Quotes.Api;
using Quotes.Api.ApiModules;
using Quotes.Api.Telemetry;
using Quotes.Application;
using Quotes.Infrastructure;
using Quotes.Infrastructure.Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    // The API versions live in their own folders, one IApiModule each; ApiModuleRegistry
    // lists them, so this file stays agnostic of which transports exist. Each module owns
    // its literal AddOpenApi call, because the XML-comment source generator only intercepts
    // calls whose document name is a string literal.
    var modules = ApiModuleRegistry.Modules;
    builder.AddStandardApiServices([.. modules.Select(module => module.DocumentName).OfType<string>()]);
    builder.Services.AddSingleton<IReadOnlyDictionary<string, OpenApiDocumentInfo>>(
        modules
            .Where(module => module.DocumentName is not null && module.DocumentInfo is not null)
            .ToDictionary(module => module.DocumentName!, module => module.DocumentInfo!));
    foreach (var module in modules)
    {
        module.AddServices(builder.Services);
    }

    builder.AddStandardJwtAuthentication(
        (QuoteScopes.ReadPolicy, QuoteScopes.ReadScope),
        (QuoteScopes.WritePolicy, QuoteScopes.WriteScope));

    // The API host is the composition root: each layer contributes its own registrations.
    builder.Services.AddQuotesApplication();
    builder.AddQuotesInfrastructure();
    builder.Services.AddQuotesUseCaseTelemetry();

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

    // Every transport resolves the same decorated use cases from the same container.
    foreach (var module in modules)
    {
        module.MapEndpoints(app);
    }

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
