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

// The quotes catalog lives in PostgreSQL, a sibling container inside the deployment.
// Deliberately ephemeral (no data volume): every run migrates and seeds from scratch,
// which is exactly the catalog the BDD and e2e suites assert on. Add WithDataVolume()
// to keep data across runs at the cost of that determinism.
var postgres = builder.AddPostgres("postgres").WithPgWeb();
var quotesDb = postgres.AddDatabase("quotesdb");

var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("https", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText })
    .WithUrlForEndpoint("http", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText });

var quotes = builder.AddProject<Projects.Quotes_Api>("quotes-api")
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithReference(quotesDb)
    .WaitFor(quotesDb)
    .WithHttpHealthCheck("/health")
    .WithUrlForEndpoint("https", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText })
    .WithUrlForEndpoint("http", ep => new() { Url = ScalarPath, DisplayText = ScalarDisplayText });

// Single entry point: YARP routes every /api prefix, and both the SPA's dev proxy
// and the published static site sit behind it — the role Traefik's `edge` plays in
// the Go sibling. The APIs stay internal; only this resource is externally published.
var gateway = builder.AddYarp("gateway")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/api/v1/auth/{**catch-all}", auth);
        // All quote API versions live in the same service; the SPA picks one at request time.
        // v3 is gRPC-JSON transcoding — plain HTTP/1.1 JSON, so YARP proxies it like the rest.
        yarp.AddRoute("/api/v0/quotes/{**catch-all}", quotes);
        yarp.AddRoute("/api/v1/quotes/{**catch-all}", quotes);
        yarp.AddRoute("/api/v2/quotes/{**catch-all}", quotes);
        yarp.AddRoute("/api/v3/quotes/{**catch-all}", quotes);
    })
    .WithExternalHttpEndpoints();

// WithPnpm: Aspire defaults to npm; the explicit call makes run mode use
// `pnpm install` + `pnpm run dev` and publish mode `pnpm install --frozen-lockfile`.
// The proxy targets reuse the API-derived variable names the Vite config already
// reads but point them at the gateway, so dev traffic crosses the same route table
// as production (the Go sibling points these same names at its Traefik edge).
var web = builder.AddViteApp("web", "../../frontend")
    .WithPnpm()
    .WithEnvironment("AUTH_API_HTTP", gateway.GetEndpoint("http"))
    .WithEnvironment("QUOTES_API_HTTP", gateway.GetEndpoint("http"))
    .WaitFor(auth)
    .WaitFor(quotes)
    .WithExternalHttpEndpoints();

// Publish mode replaces the Vite dev server: the built SPA is baked into the
// gateway image, so one port serves both static files and the API prefixes.
gateway.PublishWithStaticFiles(web);

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

await builder.Build().RunAsync();
