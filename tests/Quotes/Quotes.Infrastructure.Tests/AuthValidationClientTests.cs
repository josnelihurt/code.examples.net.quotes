using System.Net;
using AspireQuotesPoc.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Quotes.Infrastructure.Tests;

public class AuthValidationClientTests
{
    private static AuthValidationClient CreateClient(StubHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://auth-api") },
            NullLogger<AuthValidationClient>.Instance);

    [Fact]
    public async Task A_valid_response_carries_the_username_through()
    {
        var handler = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK,
            """{"valid":true,"username":"jrb"}""");

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeTrue();
        result.Username.ShouldBe("jrb");
    }

    [Fact]
    public async Task The_bearer_token_and_correlation_id_are_sent_with_the_request()
    {
        var handler = StubHttpMessageHandler.Returning(
            HttpStatusCode.OK,
            """{"valid":true,"username":"jrb"}""");

        await CreateClient(handler).ValidateAsync("token", "corr-42", TestContext.Current.CancellationToken);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.ShouldBe("/api/auth/validate");
        handler.LastRequest.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("token");
        handler.LastRequest.Headers.GetValues(HttpHeaderNames.CorrelationId).ShouldContain("corr-42");
        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody.ShouldContain("token");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_non_success_status_is_reported_as_invalid(HttpStatusCode statusCode)
    {
        var handler = StubHttpMessageHandler.Returning(statusCode);

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
        result.Username.ShouldBeNull();
    }

    [Fact]
    public async Task A_response_saying_the_token_is_invalid_is_honoured()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{"valid":false}""");

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task A_null_json_body_is_reported_as_invalid()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "null");

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task Malformed_json_is_swallowed_and_reported_as_invalid()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{not json");

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task A_transport_failure_is_reported_as_invalid_rather_than_thrown()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("auth-api unreachable"));

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
    }

    [Fact]
    public async Task A_timeout_is_reported_as_invalid_rather_than_thrown()
    {
        var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException("timed out"));

        var result = await CreateClient(handler).ValidateAsync("token", "corr", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
    }
}
