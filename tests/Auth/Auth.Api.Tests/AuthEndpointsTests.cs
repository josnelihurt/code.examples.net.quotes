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
    private static readonly ILogger<AuthEndpointsLog> _logger = NullLogger<AuthEndpointsLog>.Instance;

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
            TestContext.Current.CancellationToken);

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Validate_accepts_a_token_supplied_in_the_body()
    {
        _authService.ValidateAsync("body-token", Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(true, "jrb"));

        var result = await AuthEndpoints.ValidateAsync(
            new ValidateRequestDto { AccessToken = "body-token" },
            _authService,
            new DefaultHttpContext(),
            _logger,
            TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Valid.ShouldBeTrue();
        ok.Value.Username.ShouldBe("jrb");
    }

    [Fact]
    public async Task Validate_falls_back_to_the_authorization_header()
    {
        _authService.ValidateAsync("header-token", Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(true, "jrb"));

        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer header-token";

        var result = await AuthEndpoints.ValidateAsync(
            body: null, _authService, http, _logger, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        await _authService.Received(1).ValidateAsync("header-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Validate_prefers_the_body_token_over_the_header()
    {
        _authService.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(true, "jrb"));

        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer header-token";

        await AuthEndpoints.ValidateAsync(
            new ValidateRequestDto { AccessToken = "body-token" },
            _authService,
            http,
            _logger,
            TestContext.Current.CancellationToken);

        await _authService.Received(1).ValidateAsync("body-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Validate_returns_a_400_problem_without_calling_the_service_when_no_token_is_present()
    {
        var result = await AuthEndpoints.ValidateAsync(
            body: null,
            _authService,
            new DefaultHttpContext(),
            _logger,
            TestContext.Current.CancellationToken);

        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(StatusCodes.Status400BadRequest);
        await _authService.DidNotReceiveWithAnyArgs().ValidateAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Validate_answers_200_with_valid_false_when_the_token_is_rejected()
    {
        _authService.ValidateAsync("stale", Arg.Any<CancellationToken>())
            .Returns(new ValidateResult(false, null));

        var result = await AuthEndpoints.ValidateAsync(
            new ValidateRequestDto { AccessToken = "stale" },
            _authService,
            new DefaultHttpContext(),
            _logger,
            TestContext.Current.CancellationToken);

        var ok = result.ShouldBeOfType<Ok<ValidateResponseDto>>();
        ok.StatusCode.ShouldBe(StatusCodes.Status200OK);
        ok.Value.ShouldNotBeNull();
        ok.Value.Valid.ShouldBeFalse();
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
