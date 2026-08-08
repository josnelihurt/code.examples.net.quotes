using Auth.Api;
using Auth.Api.Contracts;
using Auth.Application;
using Auth.Infrastructure;
using AspireQuotesPoc.Telemetry;
using FluentValidation;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();
    builder.AddStandardApiServices();
    builder.Services.AddAuthInfrastructure();
    builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseSerilogDefaults();
    app.UseCorrelationId();
    app.MapDefaultEndpoints();
    app.MapStandardApiDocumentation();

    var auth = app.MapGroup("/api/auth").WithTags("Auth");

    auth.MapPost("/login", async (LoginRequestDto body, IAuthService authService, HttpContext http, ILogger<Program> logger) =>
    {
        var validation = await ValidationFilter.ValidateAsync(body, http);
        if (validation is not null)
        {
            return validation;
        }

        var correlationId = http.GetCorrelationId();
        logger.LogInformation("Login attempt for user {Username}", body.Username);

        var result = authService.Login(new LoginRequest(body.Username, body.Password));
        if (result is null)
        {
            AppMetrics.Record(AppMetrics.AuthLoginCount, "failure");
            logger.LogWarning("Login failed for user {Username}", body.Username);
            return Results.Json(new ErrorResponseDto { Error = "Invalid credentials" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        AppMetrics.Record(AppMetrics.AuthLoginCount, "success");
        logger.LogInformation("Login succeeded for user {Username}", result.Username);
        return Results.Ok(new LoginResponseDto
        {
            AccessToken = result.AccessToken,
            CorrelationId = correlationId,
            ExpiresIn = result.ExpiresIn,
            Username = result.Username
        });
    })
    .WithName("Login")
    .Produces<LoginResponseDto>(StatusCodes.Status200OK)
    .Produces<ErrorResponseDto>(StatusCodes.Status401Unauthorized)
    .ProducesValidationProblem();

    auth.MapPost("/validate", async (ValidateRequestDto? body, IAuthService authService, HttpContext http, ILogger<Program> logger) =>
    {
        var token = body?.AccessToken;
        if (string.IsNullOrWhiteSpace(token)
            && http.Request.Headers.Authorization.FirstOrDefault() is { } header
            && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = header["Bearer ".Length..].Trim();
        }

        var result = authService.Validate(token ?? string.Empty);
        if (!result.Valid)
        {
            AppMetrics.Record(AppMetrics.AuthValidateCount, "failure");
            logger.LogWarning("Token validation failed");
            return Results.Json(new ValidateResponseDto { Valid = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        AppMetrics.Record(AppMetrics.AuthValidateCount, "success");
        logger.LogInformation("Token validated for user {Username}", result.Username);
        await Task.CompletedTask;
        return Results.Ok(new ValidateResponseDto { Valid = true, Username = result.Username });
    })
    .WithName("ValidateToken")
    .Produces<ValidateResponseDto>(StatusCodes.Status200OK)
    .Produces<ValidateResponseDto>(StatusCodes.Status401Unauthorized);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Auth.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
