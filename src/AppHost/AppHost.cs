using Microsoft.Extensions.Logging;

const string ScalarDisplayText = "Scalar";
const string ScalarPath = "/scalar";
const string ScalarDocsPath = "/scalar/";

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

// One secret shared by both APIs (Auth signs, Quotes verifies). Local runs get a
// generated value from the dashboard; published output exposes it as a compose
// variable that operators must fill with a real secret.
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText })
    .WithUrlForEndpoint("http", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText });

var quotes = builder.AddProject<Projects.Quotes_Api>("quotes-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText })
    .WithUrlForEndpoint("http", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText });

// WithPnpm: Aspire defaults to npm; the explicit call makes run mode use
// `pnpm install` + `pnpm run dev` and publish mode `pnpm install --frozen-lockfile`.
var web = builder.AddViteApp("web", "../../frontend")
    .WithPnpm()
    .WithReference(auth)
    .WithReference(quotes)
    .WaitFor(auth)
    .WaitFor(quotes)
    .WithExternalHttpEndpoints();

// Docsify documentation site (appears in Aspire dashboard).
builder.AddExecutable("docs", "pnpm", "../..", "dlx", "docsify-cli", "serve", "docs", "-p", "3001", "-H", "0.0.0.0")
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
        yarp.AddRoute("/api/v1/auth/{**catch-all}", auth);
        // Both quote API versions live in the same service; the SPA picks one at request time.
        yarp.AddRoute("/api/v0/quotes/{**catch-all}", quotes);
        yarp.AddRoute("/api/v1/quotes/{**catch-all}", quotes);
    })
    .WithExternalHttpEndpoints()
    .PublishWithStaticFiles(web);

await builder.Build().RunAsync();
