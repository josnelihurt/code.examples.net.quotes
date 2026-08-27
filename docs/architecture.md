# Architecture

```text
Browser -> Vite (web) --proxy--> YARP gateway (/api/*)
                                   -> Auth.Api (/api/v1/auth/*)
                                   -> Quotes.Api (/api/v0..v3/quotes/*)
Quotes.Api validates JWT locally (JwtBearer middleware)
Auth.Api POST /api/v1/auth/validate remains for optional introspection
Aspire AppHost orchestrates processes + YARP gateway (single entry point) + Docsify
```

## Detailed component docs

This page states the **rules**. The diagrams and the per-project detail live elsewhere:

- [System design](system-design.md) — deployment topology, component diagram, request lifecycle, CI pipeline.
- Per-project documents next to the source: [AppHost](../src/AppHost/README.md), [ServiceDefaults](../src/ServiceDefaults/README.md), [Auth](../src/Auth/README.md), [Quotes](../src/Quotes/README.md), [frontend](https://github.com/josnelihurt/code.examples.frontend.quotes) (a pinned submodule — its README lives in its own repository). Each layer folder carries its own `README.md` with its types, invariants and DDD rationale.

## Projects

| Path / resource | Role |
|-----------------|------|
| `src/AppHost` (stack orchestration) | Aspire AppHost |
| `src/ServiceDefaults` | Shared Serilog, OTEL, Scalar/OpenAPI, JwtBearer auth + scope policies, ErrorOr→ProblemDetails mapping, correlation |
| `src/Auth` → `auth-api` | Login + JWT issue/validate (DDD layers) |
| `src/Quotes` → `quotes-api` | Random quote; JwtBearer protects `/api/v1/quotes` |
| `web` | React + TypeScript Vite SPA |
| `gateway` | YARP routes `/api/v1/auth` and all quote API versions (`/api/v0..v3/quotes`); single entry point in run and publish, serving the static SPA on publish |
| `docs` | Docsify + combined Scalar reference |

## Correlation

Header `X-Correlation-Id` is created or accepted on each request, returned from login, and reused by the UI on quote calls. Serilog and OTEL scopes/tags carry the same id.

## Bounded context shape rules

The two contexts must answer every structural question the same way; these rules are enforced mechanically by `tests/Architecture.Tests` (NetArchTest).

**Canonical shape.** When the two contexts answer a structural question differently, the **Quotes** answer is canonical — copy it for the next service. Auth is deliberately labeled the **thin-context variant**: it demonstrates the minimum a context with no domain invariants needs (one multi-method application service, a two-file Domain, an Application-level error catalog, a Singleton service). The differences are documented, not accidental: per-intent use-case interfaces + `Command`/`Query` records + Domain-rooted error catalog + `Mapping/` (Quotes) vs one `IAuthService` + `Request` records + Application-level errors and inline mapping (Auth). Lifetimes follow rule 4 in both. The scope vocabulary follows the same principle: ServiceDefaults registers scope policies **parameterized** (`AddStandardJwtAuthentication((policy, scope), …)`) and carries no context vocabulary — each API declares its own scopes at composition (`Quotes.Api.QuoteScopes`), and `Architecture.Tests` pins the resource-side and mint-side spellings together.

1. **Project shape**: Domain / Application / Infrastructure / Api per context. A Domain project exists only when the context owns invariants (Auth's is a single port today because tokens have no domain rules yet — add types there, not to Application, when they appear).
2. **Port placement**: repository-style ports (persistence, external state) live in `*.Domain.Abstractions`; technical ports (token minting, machine concerns) live in `*.Application.Abstractions`. Adapters implement them in Infrastructure.
3. **Dependency direction**: Domain depends on nothing; Application depends on its own Domain; Infrastructure on Domain + Application; the Api host composes Application + Infrastructure and never references Domain types. Bounded contexts never reference each other; ServiceDefaults references no context.
4. **Service lifetimes**: register use cases and their decorator chains as **Scoped** by default; Singleton only for adapters proven stateless (credential store, token service). One lifetime rule per seed, not per context.
5. **API versioning**: every context versions its surface from its first endpoint (`/api/v1/...`). Auth's original unversioned `/api/auth` moved to `/api/v1/auth` with the OpenAPI documentation work — its first breaking change.
6. **Value objects** implement `IEquatable<T>` with ordinal value equality — the glossary's "equality by value" is code, not aspiration.

## Collections and pagination

`GET /api/v1/quotes` is the ratified list pattern: 1-based `page` + `pageSize` query parameters (defaults `1` / `20`, maximum `100`, violations answer `400 quote.invalid_page_request`), offset arithmetic in the use case, `ListAsync(skip, take)` on the repository port returning `QuotePage(Items, Total)`, and a response carrying `items`, `page`, `pageSize`, `totalItems`, `totalPages`. Pages follow stable catalog order; offsets beyond the end return an empty page, never an error. New collection endpoints copy this shape instead of inventing cursors or offsets ad hoc.

## API versions and transport styles

The quote catalog is served four times, from one core:

| | `v0` | `v1` | `v2` | `v3` |
|---|---|---|---|---|
| Transport | ASP.NET MVC controllers | Minimal APIs | Generated gRPC service + HTTP adapter | Stock gRPC-JSON transcoding |
| Contract source | C# DTOs | C# DTOs | `V2/Contracts/quotes_v2.proto` (Grpc.Tools codegen) | `V3/Contracts/quotes_v3.proto` (annotations drive routing) |
| Entry point | `V0/Controllers/QuotesController.cs` | `V1/Endpoints/QuoteEndpoints.cs` | `V2/Endpoints/QuoteEndpoints.cs` | `V3/Services/QuoteGrpcService.cs` |
| Routing | `[Route]` / `[HttpGet]` attributes | `MapGroup` + `MapGet`/`MapPost` | Adapter mirrors the `google.api.http` rules | The `google.api.http` rules themselves |
| Result type | `ActionResult<T>` | `IResult` | `IResult` (JSON-PB formatted) | transcoded `IMessage` replies |
| Error mapping | `ToActionResult` | `ToProblem` | `RpcException` bridge → `ToProblem` | `RpcException` → gRPC status envelope |
| Create answers | `201` + `Location` | `201` + `Location` | `201` + `Location` | `200`, no `Location` |
| OpenAPI document | `/openapi/v0.json` | `/openapi/v1.json` | `/openapi/v2.json` (descriptor-built schemas) | `/openapi/v3.json`, generated from the proto and served verbatim |

`v0` is not an earlier release. It is the older *style*, kept alongside `v1` so the two can be
compared on identical behaviour — the React client has a version switch for exactly this. Read
`v0` as "the way a classic MVC service does it", not "the deprecated one". `v2` and `v3` are the
proto-first pair — same contract artifact, two runtimes — compared in detail in
[proto-transports.md](proto-transports.md): `v2` keeps byte-level parity with v0/v1 through an
explicit adapter, while `v3` shows what the platform runtime does by itself (and where it
deliberately drifts).

Everything below the transport is shared verbatim: the same four use cases, the same decorator
chain, the same repository, the same JWT scope policies. Neither version's DTOs nor mappers are
visible to the other — versions own their contracts, so one can change shape without dragging the
other along, which is the point of versioning them separately in the first place.

What makes this more than a curiosity is that v0, v1 and v2 are held to *byte-level* response parity by
`tests/Quotes/Quotes.Api.Tests/VersionParityTests.cs`, which drives every version pair through the
real host and compares status, media type and body. `v2` earns its place in that set through the
proto adapter: errors thrown as `RpcException` carry every ErrorOr field across the service
boundary (trailer metadata), and the adapter rebuilds them through the same shared factory, so the
problem bodies cannot drift. `v3` is deliberately outside the parity set — its drift is pinned by
its own wire tests and the `TranscodedQuotes.feature` spec. Two details are load-bearing there:

- `ErrorOrMvcExtensions.ToActionResult` builds its payload from the same
  `ProblemDetailsFactory` as `ToProblem`, and writes it through the same `IProblemDetailsService`
  (`ProblemDetailsActionResult`). A plain `ObjectResult` would answer `application/json` and drop
  the `traceId` that minimal APIs emit.
- `AddStandardControllers` replaces the `[ApiController]` automatic 400 via **`PostConfigure`**,
  because MVC's own `ApiBehaviorOptionsSetup` would otherwise overwrite a plain `Configure`.

Adding a version means: a folder under the API host with its own contracts, mappers, entry
points and an `IApiModule` (see `ApiModules/`) plus one line in `ApiModuleRegistry`'s
explicit list; `Program.cs` needs no edit. The module owns a group name tagging the version into its own
OpenAPI document (`.WithGroupName` for minimal APIs, `[ApiExplorerSettings(GroupName = ...)]`
for controllers) and its own literal `AddOpenApi("...", o => o.ConfigureStandardOpenApi("..."))`
call — the literal is what the XML-comment source generator intercepts, so it cannot be looped
over. Then: a gateway route in `AppHost.cs`; and a frozen contract under `docs/openapi/` — a
document-serving version regenerates its YAML there via `./scripts/update-contracts.sh`; a
transcoding version generates its document from its proto in the same pipeline (buf +
protoc-gen-openapiv2, see the v3 contract). Each contract is self-contained: a new version
does not touch the frozen documents of the versions beside it.
No Application, Domain or Infrastructure change is involved — if one turns out to
be needed, the layering has sprung a leak.

## Authentication

Quotes uses `AddStandardJwtAuthentication` / `UseStandardAuthentication` from ServiceDefaults (JwtBearer + `RequireAuthorization` on the `/api/v1/quotes` group; reads require the `quotes:read` scope policy and writes the `quotes:write` policy, so a valid token alone grants nothing). Auth and Quotes share the same `Jwt` issuer, audience, and signing key — in Development it comes from user-secrets (or the Aspire `jwt-signing-key` parameter), never from committed files, and Production startup rejects the public development key. Auth `POST /api/v1/auth/validate` is an RFC 7662-style introspection endpoint (invalid tokens answer `200 {valid: false}`; only a missing token is a 400); Quotes no longer calls it per request.

Scope differentiation is real, not decorative: the credential store returns the granted scopes with every successful validation (`CredentialValidationResult`), and the token service mints exactly those claims — `jrb` holds read+write, `reader` holds read-only, so a 403 is reachable by any client of the seed, not only by hand-minted test tokens. The scaffolding credential store refuses to register in Production (startup-time, same stance as the dev signing key), and the public auth endpoints sit behind a fixed-window rate limiter (per client IP; over-limit answers `429` as ProblemDetails with `errorCode auth.rate_limited`).

## Error flow

Expected failures are `ErrorOr` results from Domain/Application, mapped once at the edge to RFC 9457 ProblemDetails (`ErrorOrHttpExtensions.ToProblem` for minimal APIs, `ErrorOrMvcExtensions.ToActionResult` for controllers — both build the payload from the same `ProblemDetailsFactory`): `errorCode` + `correlationId` extensions, validation errors under `errors`, `ErrorType` deciding the status code. Exceptions are reserved for infrastructure faults and handled by `UseExceptionHandler`.

Result branching uses the ErrorOr combinators rather than manual `IsError` checks: `Switch`/`SwitchFirst` for side effects (the telemetry/logging decorators are the reference implementation) and `Match`/`MatchFirst` for mapping to another value (outcome tags, endpoint `IResult`s — `Match`'s error payload is the `List<Error>` that `ToProblem` extends). Plain early returns remain correct for one-branch flows (`if (quote is null) return QuoteErrors.NotFound;`) and for non-ErrorOr results such as auth's `ValidateResult`.

## Cross-cutting telemetry

Operation metrics and structured logging live in decorator chains wired at the composition root (`Telemetry/` in each API host), not in endpoint handlers or use cases: `AddQuotesUseCaseTelemetry` / `AddAuthServiceTelemetry` resolve each use case / the auth service as `Telemetry → Logging → inner`, so handlers only map routes and results. Counter names and outcome tags are contract (see observability.md). The one endpoint-side exception is the auth validate missing-token rejection, recorded inline because bearer parsing is an API concern that fails before the service is invoked.

## Resilience

Global HttpClient defaults enable Aspire service discovery only. Outbound clients that need Polly should add `Microsoft.Extensions.Http.Resilience` explicitly per client when the first service-to-service call appears — this base does not ship a speculative helper.
