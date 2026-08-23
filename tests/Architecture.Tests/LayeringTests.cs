using AspireQuotesPoc.ServiceDefaults.Telemetry;
using Auth.Api.Endpoints;
using Auth.Application;
using Auth.Infrastructure;
using NetArchTest.Rules;
using Quotes.Api.V1.Endpoints;
using Quotes.Application;
using Quotes.Domain;
using Quotes.Infrastructure;

namespace Architecture.Tests;

/// <summary>
/// Mechanical enforcement of the layering table in the README: project references are
/// the architecture, and this suite makes drift a red test instead of a review comment.
/// </summary>
public class LayeringTests
{
    private const string _quotesDomain = "Quotes.Domain";
    private const string _quotesApplication = "Quotes.Application";
    private const string _quotesInfrastructure = "Quotes.Infrastructure";
    private const string _quotesApi = "Quotes.Api";
    private const string _authDomain = "Auth.Domain";
    private const string _authApplication = "Auth.Application";
    private const string _authInfrastructure = "Auth.Infrastructure";
    private const string _authApi = "Auth.Api";

    [Fact]
    public void Domain_layers_depend_on_no_project()
    {
        var result = Types.InAssemblies([typeof(Quote).Assembly, typeof(Auth.Domain.Abstractions.ICredentialStore).Assembly])
            .ShouldNot()
            .HaveDependencyOnAny(
                _quotesApplication, _quotesInfrastructure, _quotesApi,
                _authApplication, _authInfrastructure, _authApi,
                "AspireQuotesPoc.ServiceDefaults")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join("\n", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_layers_depend_only_on_their_own_domain()
    {
        var quotes = Types.InAssembly(typeof(CreateQuoteUseCase).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(_quotesInfrastructure, _quotesApi, _authDomain, _authApplication, _authInfrastructure, _authApi)
            .GetResult();
        var auth = Types.InAssembly(typeof(AuthService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(_authInfrastructure, _authApi, _quotesDomain, _quotesApplication, _quotesInfrastructure, _quotesApi)
            .GetResult();

        quotes.IsSuccessful.ShouldBeTrue(string.Join("\n", quotes.FailingTypeNames ?? []));
        auth.IsSuccessful.ShouldBeTrue(string.Join("\n", auth.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_layers_depend_on_domain_and_application_only()
    {
        var quotes = Types.InAssembly(typeof(PostgresQuoteRepository).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(_quotesApi, _authDomain, _authApplication, _authInfrastructure, _authApi)
            .GetResult();
        var auth = Types.InAssembly(typeof(JwtTokenService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(_authApi, _quotesDomain, _quotesApplication, _quotesInfrastructure, _quotesApi)
            .GetResult();

        quotes.IsSuccessful.ShouldBeTrue(string.Join("\n", quotes.FailingTypeNames ?? []));
        auth.IsSuccessful.ShouldBeTrue(string.Join("\n", auth.FailingTypeNames ?? []));
    }

    [Fact]
    public void Api_hosts_compose_through_application_and_infrastructure_never_domain()
    {
        var quotes = Types.InAssembly(typeof(QuoteEndpoints).Assembly)
            .ShouldNot()
            .HaveDependencyOn(_quotesDomain)
            .GetResult();
        var auth = Types.InAssembly(typeof(AuthEndpoints).Assembly)
            .ShouldNot()
            .HaveDependencyOn(_authDomain)
            .GetResult();

        quotes.IsSuccessful.ShouldBeTrue(string.Join("\n", quotes.FailingTypeNames ?? []));
        auth.IsSuccessful.ShouldBeTrue(string.Join("\n", auth.FailingTypeNames ?? []));
    }

    [Fact]
    public void Bounded_contexts_never_reference_each_other()
    {
        var quotesSide = Types.InAssemblies([typeof(Quote).Assembly, typeof(CreateQuoteUseCase).Assembly, typeof(PostgresQuoteRepository).Assembly, typeof(QuoteEndpoints).Assembly])
            .ShouldNot()
            .HaveDependencyOnAny(_authDomain, _authApplication, _authInfrastructure, _authApi)
            .GetResult();
        var authSide = Types.InAssemblies([typeof(Auth.Domain.Abstractions.ICredentialStore).Assembly, typeof(AuthService).Assembly, typeof(JwtTokenService).Assembly, typeof(AuthEndpoints).Assembly])
            .ShouldNot()
            .HaveDependencyOnAny(_quotesDomain, _quotesApplication, _quotesInfrastructure, _quotesApi)
            .GetResult();

        quotesSide.IsSuccessful.ShouldBeTrue(string.Join("\n", quotesSide.FailingTypeNames ?? []));
        authSide.IsSuccessful.ShouldBeTrue(string.Join("\n", authSide.FailingTypeNames ?? []));
    }

    [Fact]
    public void ServiceDefaults_is_a_platform_kit_not_a_context()
    {
        var result = Types.InAssembly(typeof(AppMetrics).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(_quotesDomain, _quotesApplication, _quotesInfrastructure, _quotesApi, _authDomain, _authApplication, _authInfrastructure, _authApi)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join("\n", result.FailingTypeNames ?? []));
    }
}
