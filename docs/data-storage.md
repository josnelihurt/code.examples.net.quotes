# Data storage and connections

How the quotes catalog went from an in-memory list to a real PostgreSQL engine running as a sibling
container in the deployment, how every service gets its connection, and what a service written in
another language would consume to reach the same data.

## The shape

```mermaid
flowchart LR
    subgraph AppHost orchestration
        PG["postgres container\n(image pinned by Aspire)"]
        PGWEB["pgweb\n(browse the catalog)"]
        DB[("quotesdb database")]
        API["quotes-api\n(ASP.NET Core)"]
        PG --- DB
        PGWEB --- PG
    end
    API -- "WithReference(quotesdb)" --> DB
```

In [AppHost.cs](../src/AppHost/AppHost.cs):

```csharp
var postgres = builder.AddPostgres("postgres").WithPgWeb();
var quotesDb = postgres.AddDatabase("quotesdb");

builder.AddProject<Projects.Quotes_Api>("quotes-api")
    .WithReference(quotesDb)   // injects the connection as an env var
    .WaitFor(quotesDb);        // don't start until the database is healthy
```

`AddPostgres` declares a PostgreSQL server container inside the deployment — locally a podman
container, and whatever the publish target (the compose environment the AppHost already declares)
maps it to. The database is deliberately **ephemeral** (no `WithDataVolume()`): every run migrates
and seeds from scratch, which is exactly the deterministic catalog the BDD and e2e suites assert
on. Persisting across runs is a one-line change (`postgres.WithDataVolume()`) at the cost of that
determinism. `WithPgWeb()` adds the lightweight pgweb UI to the dashboard for browsing the catalog.

## How the connection flows

1. **The AppHost owns the address and credentials.** Nothing is hardcoded. At run time Aspire
   generates a password, starts the container, and knows the server's endpoint *as seen from inside
   the deployment network*.
2. **`WithReference(quotesDb)` hands it to the consumer.** For a .NET project this materializes as
   the environment variable `ConnectionStrings__quotesdb` — the double underscore is .NET
   configuration's separator, so the app reads `ConnectionStrings:quotesdb`.
3. **The client integration resolves it by name.** In
   [Quotes.Infrastructure/DependencyInjection.cs](../src/Quotes/Quotes.Infrastructure/DependencyInjection.cs):

   ```csharp
   builder.AddNpgsqlDbContext<QuotesDbContext>("quotesdb");
   ```

   `"quotesdb"` is the connection name — it must match the resource name the AppHost registered.
   The Aspire client integration (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`) builds the
   `DbContext` registration from that key **and** layers on the production concerns: a pooled
   `NpgsqlDataSource`, a health check that feeds the API's `/health` endpoint, OpenTelemetry tracing
   visible in the dashboard, and connection resiliency.
4. **`WaitFor(quotesDb)` orders the boot.** quotes-api does not start until the database resource
   reports healthy, so the startup migration (below) never races a container still initializing.

The same flow covers every other Aspire data integration (MySQL, MongoDB, Redis, …): declare the
resource, `AddDatabase` for a named database on it, `WithReference` from consumers, `AddXxx` client
package with the same connection name in the consumer.

## The schema and its migrations, in code

The schema is a compile-time fact in
[Quotes.Infrastructure/Persistence/QuotesDbContext.cs](../src/Quotes/Quotes.Infrastructure/Persistence/QuotesDbContext.cs):
table `quotes`, key on `Id`, column lengths mirroring the domain's limits, a **unique index on
`NormalizedFingerprint`** (near-duplicate detection is a constraint, not a discipline), and
`HasData(QuotesSeed.Records)` — the eight shipped quotes baked into the initial migration.

Applying migrations is automatic everywhere; authoring them is a one-command step when the model
changes:

```bash
# Once per model change (the tool manifest pins dotnet-ef):
dotnet ef migrations add <Name> \
  --project src/Quotes/Quotes.Infrastructure \
  --startup-project src/Quotes/Quotes.Api
```

One versioning constraint to know about: the design-time tooling —
`Microsoft.EntityFrameworkCore.Design` (referenced by Quotes.Api with `PrivateAssets`, so it
never ships) and the `dotnet-ef` tool — must match the EF Core version the
`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` package resolves transitively. The pin lives
in [`Directory.Packages.props`](../Directory.Packages.props); a mismatch surfaces as an
MSB3277 version conflict at build time, which is the signal to re-align the pin, not to
loosen it.

The generated files are committed. At boot — under the AppHost, in the standalone e2e topology, in
every test container — [Quotes.Api/Program.cs](../src/Quotes/Quotes.Api/Program.cs) runs
`Database.MigrateAsync()` before serving: it creates the database when missing, applies only
pending migrations, and EF Core 9+ takes a database-wide migration lock so replicas starting
together cannot corrupt the history. There is no environment in which a human runs a migration
step.

## Standalone boots (e2e, contract freeze)

`frontend/playwright.config.ts` boots the APIs *without* the AppHost, and `Dockerfile.build` boots
Quotes.Api inside a build container to freeze the OpenAPI documents. Both supply the same contract
the AppHost injects — a `ConnectionStrings__quotesdb` value pointing at a throwaway PostgreSQL.
Same key, same migration-at-boot, same seeded catalog.

Every surface that hands a database connection to a service:

| Surface | Connection source | Credentials |
|---------|-------------------|-------------|
| AppHost `WithReference(quotesDb)` | Aspire generates the address and password at run time and injects `ConnectionStrings__quotesdb` | The model for real secrets: never a literal; published output exposes it as a variable operators must fill |
| Standalone e2e | [`scripts/e2e.env`](../scripts/e2e.env) — the one copy of the throwaway catalog's port, user, password and database. `scripts/e2e.sh` and the CI e2e job source it to start the container; `playwright.config.ts` parses it for its webServer env | Deliberately disposable (loopback-only, guards nothing), committed on purpose. Never put a real credential in this file |
| `Dockerfile.build` export stage | A distro PostgreSQL cluster started inside the hermetic build container | Throwaway; created and consumed without ever leaving the image |
| Testcontainers fixtures (`PostgresTestDatabase`, `QuoteApiFactory`) | `_container.GetConnectionString()` from per-run containers | Random per run |

The rule the table encodes: **throwaway connections may live in exactly one file because they
guard nothing; anything that could ever carry a non-throwaway credential must come from a
parameter, never a committed literal.** The model is `jwt-signing-key`
(`builder.AddParameter("jwt-signing-key", secret: true)` in [AppHost.cs](../src/AppHost/AppHost.cs)):
if a future standalone database stops being throwaway, its credentials become an `AddParameter`
secret the same way. The `secrets-hygiene` CI job keeps the inventory honest — it fails when the
throwaway literals appear outside `scripts/e2e.env` and its allowlist.

## The polyglot door

Nothing about the deployment is .NET-only. PostgreSQL speaks one wire protocol with drivers in
every language, and Aspire's reference contract has two dialects:

- a **.NET service** gets `ConnectionStrings__quotesdb`;
- a **non-.NET service** added with `.WithReference(quotesdb)` (an `AddContainer`, an `AddGoApp`,
  an `AddNodeApp`, an `AddPythonApp` …) receives property-style variables: `QUOTESDB_HOST`,
  `QUOTESDB_PORT`, `QUOTESDB_USERNAME`, `QUOTESDB_PASSWORD`, `QUOTESDB_DATABASENAME`, and
  `QUOTESDB_URI` (`postgresql://user:password@host:port/db`), which `pgx`, `psycopg`, `node-pg`
  and friends consume directly.

So a future rewrite of the quotes backend in Go, Node, Python or Rust is an AppHost change plus a
new container — not a data-layer redesign. Two escape hatches stay open by construction:

1. **Replace the adapter:** keep the schema, implement the same four repository operations in
   another service; the SPA never learns the difference because it speaks HTTP to the API.
2. **Read the same data:** any sibling container can join the deployment network and read
   `quotesdb` through the env-var contract above (pgweb already proves this every run — it is
   nothing but a third consumer of the same reference).

The migration files themselves are plain SQL-flavored C#; `dotnet ef migrations script` exports
them as SQL for any pipeline or toolchain if the schema ever needs to be owned outside .NET.

## Local prerequisites

Running the app or the suites needs a container runtime (`scripts/env.sh` defaults to podman):

- **AppHost / BDD specs** — Aspire drives the podman CLI directly; nothing extra to configure.
- **Unit suites using Testcontainers** (`Quotes.Infrastructure.Tests`, `Quotes.Api.Tests`) — these
  speak the Docker API socket rather than the CLI. On a podman-machine setup `scripts/env.sh`
  exports `DOCKER_HOST` (pointing at the machine's socket) and disables Ryuk, whose privileges the
  machine does not grant; with Docker Desktop nothing is needed. On CI (GitHub runners) Docker is
  simply there.
- **e2e** — `scripts/e2e.sh` starts the throwaway catalog (connection values from
  `scripts/e2e.env`) and cleans it up on exit.
