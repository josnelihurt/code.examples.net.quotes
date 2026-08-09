using Microsoft.Extensions.Logging;

const string ScalarDisplayText = "Scalar";
const string ScalarPath = "/scalar";
const string ScalarDocsPath = "/scalar/";

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText })
    .WithUrlForEndpoint("http", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText });

var quotes = builder.AddProject<Projects.Quotes_Api>("quotes-api")
    .WithReference(auth)
    .WaitFor(auth)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText })
    .WithUrlForEndpoint("http", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText });

var web = builder.AddViteApp("web", "../../frontend")
    .WithReference(auth)
    .WithReference(quotes)
    .WaitFor(auth)
    .WaitFor(quotes)
    .WithExternalHttpEndpoints();

// Docsify documentation site (appears in Aspire dashboard).
builder.AddExecutable("docs", "npx", "../..", "--yes", "docsify-cli", "serve", "docs", "-p", "3001", "-H", "0.0.0.0")
    .WithHttpEndpoint(targetPort: 3001, name: "http")
    .WithExternalHttpEndpoints()
    .WithUrls(context =>
    {
        var http = context.GetEndpoint("http");
        if (http is null)
        {
            context.Logger.LogWarning("docs: http endpoint missing; skipping Scalar (combined) URL");
            return;
        }

        context.Urls.Add(new()
        {
            Url = ScalarDocsPath,
            DisplayText = ScalarDisplayText,
            Endpoint = http
        });
    });

// Deploy entry: YARP serves static SPA and routes both APIs (no Traefik).
builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/api/auth/{**catch-all}", auth);
        yarp.AddRoute("/api/quotes/{**catch-all}", quotes);
    })
    .WithExternalHttpEndpoints()
    .PublishWithStaticFiles(web);

await builder.Build().RunAsync();
