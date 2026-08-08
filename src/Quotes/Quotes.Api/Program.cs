using AspireQuotesPoc.Telemetry;
using Microsoft.Extensions.Hosting;
using Quotes.Api.Contracts;
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
    builder.AddStandardApiServices();
    builder.Services.AddQuotesInfrastructure();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogDefaults();
    app.UseCorrelationId();
    app.MapDefaultEndpoints();
    app.MapStandardApiDocumentation();

    app.MapGet("/api/quotes/random", async (
            HttpContext http,
            IGetRandomQuoteUseCase useCase,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var authHeader = http.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                AppMetrics.Record(AppMetrics.QuotesRandomCount, "failure");
                logger.LogWarning("Missing bearer token on random quote request");
                return Results.Json(new ErrorResponseDto { Error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var token = authHeader["Bearer ".Length..].Trim();
            var correlationId = http.GetCorrelationId();
            logger.LogInformation("Fetching random quote");

            var quote = await useCase.ExecuteAsync(token, correlationId, cancellationToken);
            if (quote is null)
            {
                AppMetrics.Record(AppMetrics.QuotesRandomCount, "failure");
                logger.LogWarning("Auth validation failed for random quote request");
                return Results.Json(new ErrorResponseDto { Error = "Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            AppMetrics.Record(AppMetrics.QuotesRandomCount, "success");
            logger.LogInformation("Returning quote {QuoteId}", quote.Id);
            return Results.Ok(new QuoteResponseDto
            {
                Id = quote.Id,
                Text = quote.Text,
                Author = quote.Author
            });
        })
        .WithName("GetRandomQuote")
        .WithTags("Quotes")
        .Produces<QuoteResponseDto>(StatusCodes.Status200OK)
        .Produces<ErrorResponseDto>(StatusCodes.Status401Unauthorized);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Quotes.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
