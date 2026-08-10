using AspireQuotesPoc.Http;
using Auth.Api.Contracts;
using Auth.Api.Endpoints;
using Auth.Application;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Auth.Api.Tests;

public class AuthEndpointsTests
{
    private static readonly ILoggerFactory LoggerFactory = NullLoggerFactory.Instance;

    private readonly IAuthService _authService = Substitute.For<IAuthService>();

    private static TestHost CreateHostWithValidator() => TestHost.Create(services =>
        services.AddSingleton<IValidator<LoginRequestDto>, LoginRequestDtoValidator>());

    [Fact]
    public async Task Login_returns_the_token_and_the_request_correlation_id()
    {
        _authService.Login(new LoginRequest("jrb", "supersecret"))
            .Returns(new LoginResult("issued-token", "jrb", 3600));

        using var host = CreateHostWithValidator();
        host.Context.Items[HttpHeaderNames.CorrelationId] = "corr-123";

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequestDto { Username = "jrb", Password = "supersecret" },
            _authService,
            host.Context,
            LoggerFactory);

        var ok = result.ShouldBeOfType<Ok<LoginResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.AccessToken.ShouldBe("issued-token");
        ok.Value.Username.ShouldBe("jrb");
        ok.Value.ExpiresIn.ShouldBe(3600);
        ok.Value.CorrelationId.ShouldBe("corr-123");
    }

    [Fact]
    public async Task Login_returns_401_when_the_auth_service_rejects_the_credentials()
    {
        _authService.Login(Arg.Any<LoginRequest>()).Returns((LoginResult?)null);

        using var host = CreateHostWithValidator();

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequestDto { Username = "jrb", Password = "wrong" },
            _authService,
            host.Context,
            LoggerFactory);

        var json = result.ShouldBeOfType<JsonHttpResult<ErrorResponseDto>>();
        json.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        json.Value.ShouldNotBeNull();
        json.Value.Error.ShouldBe("Invalid credentials");
    }

    [Fact]
    public async Task Login_short_circuits_on_a_validation_failure()
    {
        using var host = CreateHostWithValidator();

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequestDto { Username = "", Password = "" },
            _authService,
            host.Context,
            LoggerFactory);

        result.ShouldBeOfType<ProblemHttpResult>()
            .ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>();
        _authService.DidNotReceiveWithAnyArgs().Login(default!);
    }

    [Fact]
    public void Validate_accepts_a_token_supplied_in_the_body()
    {
        _authService.Validate("body-token").Returns(new ValidateResult(true, "jrb"));

        using var host = TestHost.Create();

        var result = AuthEndpoints.Validate(
            new ValidateRequestDto { AccessToken = "body-token" },
            _authService,
            host.Context,
            LoggerFactory);

        var ok = result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Valid.ShouldBeTrue();
        ok.Value.Username.ShouldBe("jrb");
    }

    [Fact]
    public void Validate_falls_back_to_the_authorization_header()
    {
        _authService.Validate("header-token").Returns(new ValidateResult(true, "jrb"));

        using var host = TestHost.Create();
        host.Context.Request.Headers.Authorization = "Bearer header-token";

        var result = AuthEndpoints.Validate(body: null, _authService, host.Context, LoggerFactory);

        result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        _authService.Received(1).Validate("header-token");
    }

    [Fact]
    public void Validate_prefers_the_body_token_over_the_header()
    {
        _authService.Validate(Arg.Any<string>()).Returns(new ValidateResult(true, "jrb"));

        using var host = TestHost.Create();
        host.Context.Request.Headers.Authorization = "Bearer header-token";

        AuthEndpoints.Validate(
            new ValidateRequestDto { AccessToken = "body-token" },
            _authService,
            host.Context,
            LoggerFactory);

        _authService.Received(1).Validate("body-token");
    }

    [Fact]
    public void Validate_returns_401_without_calling_the_auth_service_when_no_token_is_present()
    {
        using var host = TestHost.Create();

        var result = AuthEndpoints.Validate(body: null, _authService, host.Context, LoggerFactory);

        var json = result.ShouldBeOfType<JsonHttpResult<ValidateResponseDto>>();
        json.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        json.Value.ShouldNotBeNull();
        json.Value.Valid.ShouldBeFalse();
        _authService.DidNotReceiveWithAnyArgs().Validate(default!);
    }

    [Fact]
    public void Validate_returns_401_when_the_token_is_rejected()
    {
        _authService.Validate("stale").Returns(new ValidateResult(false, null));

        using var host = TestHost.Create();

        var result = AuthEndpoints.Validate(
            new ValidateRequestDto { AccessToken = "stale" },
            _authService,
            host.Context,
            LoggerFactory);

        var json = result.ShouldBeOfType<JsonHttpResult<ValidateResponseDto>>();
        json.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Map_registers_both_auth_routes()
    {
        var routes = TestEndpointRouteBuilder.Collect(
            AuthEndpoints.Map,
            services => services.AddSingleton(Substitute.For<IAuthService>()));

        routes.ShouldBe(["/api/auth/login", "/api/auth/validate"], ignoreOrder: true);
    }
}
