using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServiceDefaults.Tests;

public class ResilienceExtensionsTests
{
    private sealed class CountingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private static (ServiceProvider Provider, CountingHandler Handler) BuildClient(HttpStatusCode statusCode)
    {
        var handler = new CountingHandler(statusCode);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("auth")
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddAuthHttpClientResilience();

        return (services.BuildServiceProvider(), handler);
    }

    [Fact]
    public async Task A_successful_response_is_not_retried()
    {
        var (provider, handler) = BuildClient(HttpStatusCode.OK);
        using (provider)
        {
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("auth");

            using var response = await client.GetAsync(new Uri("http://auth-api/api/auth/validate"), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            handler.Calls.ShouldBe(1);
        }
    }

    [Fact]
    public async Task A_client_error_is_not_retried()
    {
        var (provider, handler) = BuildClient(HttpStatusCode.Unauthorized);
        using (provider)
        {
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("auth");

            using var response = await client.GetAsync(new Uri("http://auth-api/api/auth/validate"), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            handler.Calls.ShouldBe(1);
        }
    }

    [Fact]
    public async Task A_server_error_is_retried_up_to_the_configured_attempt_count()
    {
        var (provider, handler) = BuildClient(HttpStatusCode.ServiceUnavailable);
        using (provider)
        {
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("auth");

            using var response = await client.GetAsync(new Uri("http://auth-api/api/auth/validate"), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            handler.Calls.ShouldBe(4); // initial attempt plus three retries
        }
    }
}
