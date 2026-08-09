using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Api.Tests;

internal static class TestEndpointRouteBuilder
{
    /// <summary>
    /// Runs a route registration delegate against a throwaway host and returns the raw route patterns.
    /// Handler dependencies must be registered so minimal API parameter inference succeeds.
    /// </summary>
    public static IReadOnlyList<string> Collect(
        Func<IEndpointRouteBuilder, IEndpointRouteBuilder> map,
        Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateSlimBuilder();
        configureServices(builder.Services);

        var app = builder.Build();
        try
        {
            map(app);

            return ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
                .ToList();
        }
        finally
        {
            ((IDisposable)app).Dispose();
        }
    }
}
