using Google.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Quotes.Api.V2.Contracts;
using Quotes.Application.Abstractions;

namespace Quotes.Api.Tests.V2;

/// <summary>
/// The proto annotations only describe a REST surface; <see cref="Quotes.Api.V2.Endpoints.QuoteEndpoints.Map"/>
/// is what actually serves it. This suite builds the v2 route table the way the real host
/// does — same group, same policies — and asserts that every google.api.http rule in the
/// descriptor has a matching endpoint behind the right authorization policy. If a route is
/// edited in code but not in the proto (or vice versa), the contract test above or this one
/// fails; between them the annotation set and the route table cannot drift silently.
/// </summary>
public class ProtoConformanceTests
{
    [Fact]
    public void Every_annotated_rpc_is_served_by_a_matching_route_behind_the_annotated_scope_policy()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAuthentication().AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IGetRandomQuoteUseCase>());
        builder.Services.AddSingleton(Substitute.For<IGetQuoteByIdUseCase>());
        builder.Services.AddSingleton(Substitute.For<IListQuotesUseCase>());
        builder.Services.AddSingleton(Substitute.For<ICreateQuoteUseCase>());
        builder.Services.AddScoped<Quotes.Api.V2.Services.QuoteGrpcService>();

        var app = builder.Build();
        try
        {
            Quotes.Api.V2.Endpoints.QuoteEndpoints.Map(app);

            var endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .ToList();

            foreach (var method in QuoteService.Descriptor.Methods)
            {
                var rule = method.GetOptions().GetExtension(AnnotationsExtensions.Http);
                rule.ShouldNotBeNull($"method {method.Name} must carry a google.api.http rule");

                var verb = rule.PatternCase switch
                {
                    HttpRule.PatternOneofCase.Get => "GET",
                    HttpRule.PatternOneofCase.Post => "POST",
                    _ => throw new InvalidOperationException($"{method.Name}: unexpected pattern {rule.PatternCase}")
                };
                var pattern = rule.PatternCase == HttpRule.PatternOneofCase.Get ? rule.Get : rule.Post;

                // MapGet("")/MapPost("") render the group route with a trailing slash; the
                // annotation (and the wire contract) spell it without one.
                var endpoint = endpoints.Single(e =>
                    e.RoutePattern.RawText!.TrimEnd('/') == pattern.TrimEnd('/')
                    && e.Metadata.OfType<HttpMethodMetadata>().Single().HttpMethods.Single() == verb);

                var expectedPolicy = verb == "POST" ? QuoteScopes.WritePolicy : QuoteScopes.ReadPolicy;
                endpoint.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull()
                    .Policy.ShouldBe(expectedPolicy);
            }
        }
        finally
        {
            ((IDisposable)app).Dispose();
        }
    }
}
