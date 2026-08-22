using AspireQuotesPoc.ServiceDefaults.Http;
using Auth.Api.Contracts;
using Auth.Api.Endpoints;
using Auth.Application.Abstractions;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Auth.Api.Tests;

public class AuthEndpointsTests
{
    private static readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private readonly IAuthService _authService = Substitute.For<IAuthService>();

    [Fact]
    public async Task Login_returns_the_token_and_the_request_correlation_id()
    {
        ErrorOr<LoginResult> login = new LoginResult("issued-token", "jrb", 3600);
        _authService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(login);

        var http = new DefaultHttpContext();
        http.Items[HttpHeaderNames.CorrelationId] = "corr-123";

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequestDto { Username = "jrb", Password = "supersecret" },
            _authService,
            http,
            _loggerFactory,
            TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<LoginResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.AccessToken.ShouldBe("issued-token");
        ok.Value.Username.ShouldBe("jrb");
        ok.Value.ExpiresIn.ShouldBe(3600);
        ok.Value.CorrelationId.ShouldBe("corr-123");
    }

    [Fact]
    public async Task Login_returns_a_401_problem_when_the_auth_service_rejects_the_credentials()
    {
        ErrorOr<LoginResult> login = AuthErrors.InvalidCredentials;
        _authService.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>()).Returns(login);

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequestDto { Username = "jrb", Password = "wrong" },
            _authService,
            new DefaultHttpContext(),
            _loggerFactory,
            TestContext.Current.CancellationToken);

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Validate_accepts_a_token_supplied_in_the_body()
    {
        _authService.Validate("body-token").Returns(new ValidateResult(true, "jrb"));

        var result = AuthEndpoints.Validate(
            new ValidateRequestDto { AccessToken = "body-token" },
            _authService,
            new DefaultHttpContext(),
            _loggerFactory);

        var ok = result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Valid.ShouldBeTrue();
        ok.Value.Username.ShouldBe("jrb");
    }

    [Fact]
    public void Validate_falls_back_to_the_authorization_header()
    {
        _authService.Validate("header-token").Returns(new ValidateResult(true, "jrb"));

        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer header-token";

        var result = AuthEndpoints.Validate(body: null, _authService, http, _loggerFactory);

        result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        _authService.Received(1).Validate("header-token");
    }

    [Fact]
    public void Validate_prefers_the_body_token_over_the_header()
    {
        _authService.Validate(Arg.Any<string>()).Returns(new ValidateResult(true, "jrb"));

        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer header-token";

        AuthEndpoints.Validate(
            new ValidateRequestDto { AccessToken = "body-token" },
            _authService,
            http,
            _loggerFactory);

        _authService.Received(1).Validate("body-token");
    }

    [Fact]
    public void Validate_returns_401_without_calling_the_auth_service_when_no_token_is_present()
    {
        var result = AuthEndpoints.Validate(
            body: null, _authService, new DefaultHttpContext(), _loggerFactory);

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

        var result = AuthEndpoints.Validate(
            new ValidateRequestDto { AccessToken = "stale" },
            _authService,
            new DefaultHttpContext(),
            _loggerFactory);

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
