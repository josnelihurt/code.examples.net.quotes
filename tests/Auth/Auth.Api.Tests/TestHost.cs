using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Api.Tests;

/// <summary>
/// Minimal <see cref="HttpContext"/> plus service provider for exercising endpoint handlers
/// without spinning up a web server.
/// </summary>
internal sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _provider;

    private TestHost(ServiceProvider provider, DefaultHttpContext context)
    {
        _provider = provider;
        Context = context;
    }

    public DefaultHttpContext Context { get; }

    public static TestHost Create(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configureServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };

        return new TestHost(provider, context);
    }

    public void Dispose() => _provider.Dispose();
}
