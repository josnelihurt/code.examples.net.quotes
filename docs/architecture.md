# Architecture

```text
Browser -> Vite (web) --proxy--> Auth.Api (/api/auth/*)
                              -> Quotes.Api (/api/v1/quotes/*)
Quotes.Api validates JWT locally (JwtBearer middleware)
Auth.Api POST /api/auth/validate remains for optional introspection
Aspire AppHost orchestrates processes + YARP gateway (publish) + Docsify
```

## Projects

| Path / resource | Role |
|-----------------|------|
| `src/AppHost` (`auth` orchestration) | Aspire AppHost |
| `src/ServiceDefaults` | Shared Serilog, OTEL, Scalar/OpenAPI, JwtBearer auth + scope policies, ErrorOr→ProblemDetails mapping, correlation |
| `src/Auth` → `auth-api` | Login + JWT issue/validate (DDD layers) |
| `src/Quotes` → `quotes-api` | Random quote; JwtBearer protects `/api/v1/quotes` |
| `web` | React + TypeScript Vite SPA |
| `gateway` | YARP routes `/api/auth` and `/api/v1/quotes`; serves static SPA on publish |
| `docs` | Docsify + combined Scalar reference |

## Correlation

Header `X-Correlation-Id` is created or accepted on each request, returned from login, and reused by the UI on quote calls. Serilog and OTEL scopes/tags carry the same id.

## Bounded context shape rules

The two contexts must answer every structural question the same way; these rules are enforced mechanically by `tests/Architecture.Tests` (NetArchTest).

1. **Project shape**: Domain / Application / Infrastructure / Api per context. A Domain project exists only when the context owns invariants (Auth's is a single port today because tokens have no domain rules yet — add types there, not to Application, when they appear).
2. **Port placement**: repository-style ports (persistence, external state) live in `*.Domain.Abstractions`; technical ports (token minting, machine concerns) live in `*.Application.Abstractions`. Adapters implement them in Infrastructure.
3. **Dependency direction**: Domain depends on nothing; Application depends on its own Domain; Infrastructure on Domain + Application; the Api host composes Application + Infrastructure and never references Domain types. Bounded contexts never reference each other; ServiceDefaults references no context.
4. **Service lifetimes**: register use cases and their decorator chains as **Scoped** by default; Singleton only for adapters proven stateless (credential store, token service). One lifetime rule per seed, not per context.
5. **API versioning**: every context versions its surface from its first endpoint (`/api/v1/...`). Auth's unversioned `/api/auth` predates the rule and moves to `/api/v1/auth` at its first breaking change, not before.
6. **Value objects** implement `IEquatable<T>` with ordinal value equality — the glossary's "equality by value" is code, not aspiration.

## Collections and pagination

`GET /api/v1/quotes` is the ratified list pattern: 1-based `page` + `pageSize` query parameters (defaults `1` / `20`, maximum `100`, violations answer `400 quote.invalid_page_request`), offset arithmetic in the use case, `ListAsync(skip, take)` on the repository port returning `QuotePage(Items, Total)`, and a response carrying `items`, `page`, `pageSize`, `totalItems`, `totalPages`. Pages follow stable catalog order; offsets beyond the end return an empty page, never an error. New collection endpoints copy this shape instead of inventing cursors or offsets ad hoc.

## API versions and transport styles

The quote catalog is served twice, from one core:

| | `v0` | `v1` |
|---|---|---|
| Transport | ASP.NET MVC controllers | Minimal APIs |
| Entry point | `V0/Controllers/QuotesController.cs` | `V1/Endpoints/QuoteEndpoints.cs` |
| Routing | `[Route]` / `[HttpGet]` attributes | `MapGroup` + `MapGet`/`MapPost` |
| Result type | `ActionResult<T>` | `IResult` |
| Error mapping | `ToActionResult` | `ToProblem` |
| OpenAPI document | `/openapi/v0.json` | `/openapi/v1.json` |

`v0` is not an earlier release. It is the older *style*, kept alongside `v1` so the two can be
compared on identical behaviour — the React client has a version switch for exactly this. Read
`v0` as "the way a classic MVC service does it", not "the deprecated one".

Everything below the transport is shared verbatim: the same four use cases, the same decorator
chain, the same repository, the same JWT scope policies. Neither version's DTOs nor mappers are
visible to the other — versions own their contracts, so one can change shape without dragging the
other along, which is the point of versioning them separately in the first place.

What makes this more than a curiosity is that the two are held to *byte-level* response parity by
`tests/Quotes/Quotes.Api.Tests/VersionParityTests.cs`, which drives both versions through the real
host and compares status, media type and body. Two details are load-bearing there:

- `ErrorOrMvcExtensions.ToActionResult` builds its payload from the same
  `ProblemDetailsFactory` as `ToProblem`, and writes it through the same `IProblemDetailsService`
  (`ProblemDetailsActionResult`). A plain `ObjectResult` would answer `application/json` and drop
  the `traceId` that minimal APIs emit.
- `AddStandardControllers` replaces the `[ApiController]` automatic 400 via **`PostConfigure`**,
  because MVC's own `ApiBehaviorOptionsSetup` would otherwise overwrite a plain `Configure`.

Adding a version means: a folder under the API host with its own contracts, mappers and entry
points; a group name tagging it into its own OpenAPI document (`.WithGroupName` for minimal APIs,
`[ApiExplorerSettings(GroupName = ...)]` for controllers); the name passed to
`AddStandardApiServices`; a gateway route in `AppHost.cs`; and a frozen contract under
`docs/openapi/`. No Application, Domain or Infrastructure change is involved — if one turns out to
be needed, the layering has sprung a leak.

## Authentication

Quotes uses `AddStandardJwtAuthentication` / `UseStandardAuthentication` from ServiceDefaults (JwtBearer + `RequireAuthorization` on the `/api/v1/quotes` group; reads require the `quotes:read` scope policy and writes the `quotes:write` policy, so a valid token alone grants nothing). Auth and Quotes share the same `Jwt` issuer, audience, and signing key — in Development it comes from user-secrets (or the Aspire `jwt-signing-key` parameter), never from committed files, and Production startup rejects the public development key. Auth `POST /api/auth/validate` is an RFC 7662-style introspection endpoint (invalid tokens answer `200 {valid: false}`; only a missing token is a 400); Quotes no longer calls it per request.

Scope differentiation is real, not decorative: the credential store returns the granted scopes with every successful validation (`CredentialValidationResult`), and the token service mints exactly those claims — `jrb` holds read+write, `reader` holds read-only, so a 403 is reachable by any client of the seed, not only by hand-minted test tokens. The scaffolding credential store refuses to register in Production (startup-time, same stance as the dev signing key), and the public auth endpoints sit behind a fixed-window rate limiter (per client IP; over-limit answers `429` as ProblemDetails with `errorCode auth.rate_limited`).

## Error flow

Expected failures are `ErrorOr` results from Domain/Application, mapped once at the edge to RFC 9457 ProblemDetails (`ErrorOrHttpExtensions.ToProblem` for minimal APIs, `ErrorOrMvcExtensions.ToActionResult` for controllers — both build the payload from the same `ProblemDetailsFactory`): `errorCode` + `correlationId` extensions, validation errors under `errors`, `ErrorType` deciding the status code. Exceptions are reserved for infrastructure faults and handled by `UseExceptionHandler`.

Result branching uses the ErrorOr combinators rather than manual `IsError` checks: `Switch`/`SwitchFirst` for side effects (the telemetry/logging decorators are the reference implementation) and `Match`/`MatchFirst` for mapping to another value (outcome tags, endpoint `IResult`s — `Match`'s error payload is the `List<Error>` that `ToProblem` extends). Plain early returns remain correct for one-branch flows (`if (quote is null) return QuoteErrors.NotFound;`) and for non-ErrorOr results such as auth's `ValidateResult`.

## Cross-cutting telemetry

Operation metrics and structured logging live in decorator chains wired at the composition root (`Telemetry/` in each API host), not in endpoint handlers or use cases: `AddQuotesUseCaseTelemetry` / `AddAuthServiceTelemetry` resolve each use case / the auth service as `Telemetry → Logging → inner`, so handlers only map routes and results. Counter names and outcome tags are contract (see observability.md). The one endpoint-side exception is the auth validate missing-token rejection, recorded inline because bearer parsing is an API concern that fails before the service is invoked.

## Resilience

Global HttpClient defaults enable Aspire service discovery only. Outbound clients that need Polly should add `Microsoft.Extensions.Http.Resilience` explicitly per client when the first service-to-service call appears — this base does not ship a speculative helper.
