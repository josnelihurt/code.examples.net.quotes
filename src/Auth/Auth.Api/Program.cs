using Auth.Api.Endpoints;
using Auth.Application;
using Auth.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    builder.AddStandardApiServices();

    // The API host is the composition root: each layer contributes its own registrations.
    builder.Services.AddAuthApplication();
    builder.Services.AddAuthInfrastructure();
    builder.Services.AddValidation();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogDefaults();
    app.UseCorrelationId();
    app.MapDefaultEndpoints();
    app.MapStandardApiDocumentation();

    AuthEndpoints.Map(app);

    await app.RunAsync();
}
// S2139: the log-and-rethrow is deliberate. Serilog is flushed in the finally block, so the
// fatal entry has to be written before the exception leaves the host.
#pragma warning disable S2139
catch (Exception ex)
{
    Log.Fatal(ex, "Auth.Api terminated unexpectedly");
    throw;
}
#pragma warning restore S2139
finally
{
    await Log.CloseAndFlushAsync();
}
