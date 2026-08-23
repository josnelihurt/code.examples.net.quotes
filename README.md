# Aspire Quotes

**Microservice seed** for teams starting .NET services on Aspire: Clean Architecture layers, a shared platform kit (`ServiceDefaults`), and a small quotes domain so the shape stays readable.

Stack: **.NET 10**, **Aspire 13**, **React + TypeScript (Vite)**, **Podman**.

## Intention

This repository is a **cloneable service base**. Copy Auth/Quotes structure and reuse `ServiceDefaults`; fill in business rules for the next service.

Quotes and login stay deliberately small. The deliverable is the shape teams inherit:

- Clean Architecture (`Api` / `Application` / `Domain` / `Infrastructure`)
- Shared platform defaults (auth, correlation, ProblemDetails, OpenAPI/Scalar, telemetry)
- Stable HTTP and error contracts other teams can depend on
- Domain modeling (entities, value objects, ports) visible while the sample is still small

Hardcoded users and an in-memory catalog are **local scaffolding** so the foundation runs offline. They are not the model for production identity or storage.

### What success looks like

Someone cloning this for a new service should be able to:

1. Copy the `Api` / `Application` / `Domain` / `Infrastructure` layout.
2. Reuse `ServiceDefaults` for auth, correlation, ProblemDetails, OpenAPI/Scalar, and telemetry.
3. Put business rules in Domain, use cases in Application, adapters in Infrastructure.
4. Ship a thin Minimal API that mostly maps request → use case → response.

The quotes catalog is the **example**; the base is the **deliverable**.

### Goals

- **Clear separations** — Domain and Application stay free of HTTP, auth headers, and status codes; Infrastructure owns adapters; Api owns transport, mapping, and OpenAPI.
- **Shared platform** — Cross-cutting concerns live in `ServiceDefaults` so they are not reimplemented per endpoint or per service.
- **Low per-endpoint cost** — Auth, error shaping, metrics, and API docs come from conventions; a new endpoint mostly maps a route, calls a use case, and maps the result.
- **Contracts as product** — Api DTOs and OpenAPI are the public surface. Prefer org-wide error shapes and security metadata over ad-hoc response types.
- **Cloneable service shape** — Auth and Quotes show the same layering so the next microservice copies structure, not policy invention.

### Layering (dependency rule)

| Tier | Owns | Must not own |
|------|------|--------------|
| **Platform** (`ServiceDefaults`) | Auth integration patterns, correlation, OpenAPI conventions, telemetry, resilience | Business rules |
| **Service host** (`*.Api`) | Composition root, endpoints, transport DTOs, mapping to/from Application | Persistence details, remote client internals |
| **Application** | Use cases, ports | `HttpContext`, bearer parsing, OpenAPI, status codes |
| **Domain** | Entities and domain ports | HTTP, DI containers, infrastructure SDKs |
| **Infrastructure** | Repositories, HTTP clients, external systems | Endpoint contracts, Swagger UI concerns |

### Domain terms

| Term | Meaning | In this project |
|------|---------|-----------------|
| **Entity** | Domain object with identity and invariants | [`Quotes.Domain.Quote`](src/Quotes/Quotes.Domain/Quote.cs) — created via `Quote.Create`, composes value objects and owns the `AuthorEqualsText` rule |
| **Value object** | No identity; equality by value | [`QuoteText`](src/Quotes/Quotes.Domain/QuoteText.cs), [`QuoteAuthor`](src/Quotes/Quotes.Domain/QuoteAuthor.cs), [`QuoteFingerprint`](src/Quotes/Quotes.Domain/QuoteFingerprint.cs) |
| **Aggregate** | Consistency boundary around a root entity | `Quote` is the aggregate root; repositories load/save the root (`IQuoteRepository`) |
| **Persistence model** | Storage shape, mapping, DB concerns | [`QuoteRecord`](src/Quotes/Quotes.Infrastructure/Persistence/QuoteRecord.cs) in Infrastructure — never put EF attributes on Domain `Quote` |

**Rule of thumb:** Domain speaks `Quote`; Infrastructure maps `Quote` ↔ `QuoteRecord` at the repository boundary. Api DTOs are transport only.

Transport vs domain validation: DTOs keep shallow guards (`[Required]`, `[MaxLength]`); the domain owns catalog invariants.

### Conventions in place

1. Authentication at the host/platform — Quotes uses JwtBearer + `RequireAuthorization`; writes need `quotes:write` (see `JwtAuthExtensions.WriteQuotesPolicy`).
2. Thin Minimal API endpoints; Application outcomes are `ErrorOr` results mapped once to RFC 9457 ProblemDetails (`ErrorOrHttpExtensions.ToProblem`) with `errorCode` and `correlationId`. Expected failures are not exceptions. Branching on a result uses the ErrorOr combinators — `Switch`/`SwitchFirst` for side effects (decorators), `Match`/`MatchFirst` for mapping to a value (outcome tags, endpoint `IResult`s) — instead of `if (result.IsError)`/`else` chains.
3. OpenAPI conventions in the platform (Bearer scheme, standard ProblemDetails shapes for 401/403/404/409/500). Operations are documented with XML `///` comments (`<summary>`/`<remarks>`/`<param>`/`<response>`/`<example>`) that the built-in generator flows into the documents, plus per-host narratives (`OpenApiDocs`); see [docs/api.md](docs/api.md).
4. Composition root at the API host: layers register themselves (`AddQuotesApplication`, `AddQuotesInfrastructure`); Program.cs composes them. Api references Application + Infrastructure, never Domain directly.
5. Transport input validation: request DTOs use Data Annotations; each host calls `AddValidation()` so binding validates before handlers run.

## What it does today

1. **Auth API** issues a JWT for hardcoded local users — `jrb` / `supersecret` holds `quotes:read` + `quotes:write`, `reader` / `readsecret` holds `quotes:read` only — and can validate tokens via `/api/v1/auth/validate` (optional introspection). Login and validate are rate-limited (fixed window per client IP, 429 as ProblemDetails), and the scaffolding credential store refuses to register in Production.
2. **Quotes API** serves an in-memory catalog after JwtBearer validates the bearer token: `GET /api/v1/quotes/random`, `GET /api/v1/quotes/{id}`, `GET /api/v1/quotes?page=&pageSize=` (the ratified offset-pagination pattern), and `POST /api/v1/quotes` (requires `quotes:write`; rejects invalid and near-duplicate quotes). The same four operations are also served at `/api/v0/quotes/...` by MVC controllers — one core, two transport styles, held to byte-level response parity by tests. See [docs/architecture.md](docs/architecture.md#api-versions-and-transport-styles).
3. **React SPA** logs in, stores token + `X-Correlation-Id`, then fetches quotes through the Vite proxy: a random quote, the paginated catalog (`/quotes`), and publishing a new quote (`/publish`, maintainer scope only). Its API types are generated from the frozen OpenAPI document, and its components have Storybook stories smoke-built in CI.
4. **Aspire AppHost** starts everything, wires service discovery, exports OpenTelemetry to the dashboard, and publishes a **YARP** gateway (no Traefik).

```text
UI (Vite) -> Auth / Quotes
Quotes validates JWT locally (JwtBearer); Auth /validate remains for introspection
OTEL metrics/logs/traces -> Aspire dashboard
```

## Solution layout

Each `src/` row links to a component document describing that project's layers, DDD concepts and call flows. For the whole picture — deployment topology, component diagram, request lifecycle — see [docs/system-design.md](docs/system-design.md).

| Path | Role |
|------|------|
| [`src/AppHost/`](src/AppHost/README.md) | Aspire orchestration (`AspireQuotesPoc.AppHost`) |
| [`src/ServiceDefaults/`](src/ServiceDefaults/README.md) | Platform kit: Serilog, OTEL, OpenAPI/Scalar helpers, JwtBearer + scope policies, ErrorOr→ProblemDetails, correlation |
| [`src/Auth/`](src/Auth/README.md) | Auth service — Domain / Application / Infrastructure / Api |
| [`src/Quotes/`](src/Quotes/README.md) | Quotes service — Domain / Application / Infrastructure / Api |
| [`frontend/`](frontend/README.md) | React + TS Vite SPA |
| `docs/` | Docsify + combined Scalar reference |
| `contracts/` | Pointer to Docsify OpenAPI docs ([api-contracts.md](contracts/api-contracts.md)) |
| `tests/` | xUnit unit/API tests (OpenCover for Sonar) + `tests/Bdd` Reqnroll specs against the running stack |
| `scripts/` | Env, start, docs, publish, test, bdd, e2e, verify-docs, update-contracts, Sonar, bundle |

## How to run

```bash
./scripts/start.sh
```

Uses `scripts/env.sh` (`ASPIRE_CONTAINER_RUNTIME=podman`, `ASPNETCORE_ENVIRONMENT=Development`). Open the Aspire dashboard URL from the console, then the `web` endpoint.

Documentation:

```bash
./scripts/serve-docs.sh
```

API specs (Reqnroll against the running Aspire stack — YARP gateway included; needs Podman):

```bash
./scripts/bdd.sh
```

SPA end-to-end (Playwright BDD in Chromium; boots the APIs and Vite itself):

```bash
./scripts/e2e.sh
```

Scalar guidance / combined docs page:

```bash
./scripts/open-scalar.sh
```

Publish Docker Compose artifacts (Podman-compatible):

```bash
./scripts/publish.sh
```

Unit tests (.NET + Coverlet OpenCover):

```bash
./scripts/test.sh
```

C# lint — warning-level style rules incl. unused usings (IDE0005); `--fix` rewrites:

```bash
./scripts/lint.sh
```

Frontend tests (Vitest):

```bash
cd frontend && pnpm test
```

Local SonarQube (Podman) + scan:

```bash
./scripts/sonar-up.sh
SONAR_ADMIN_PASSWORD='...' ./scripts/sonar-quality-profile.sh  # once: adds S1128 (unused usings)
./scripts/sonar-scan.sh
```

Export a full git bundle to `~/repo.bundle`:

```bash
./scripts/export-bundle.sh
```

More detail in Docsify: [Testing](docs/testing.md), [SonarQube](docs/sonar.md), [ServiceDefaults as a NuGet building block](docs/servicedefaults-nuget-extraction.md), [Documentation process](docs/documentation-process.md), [Panel Review](docs/panel-review.md).

## OpenAPI / Scalar

[Scalar](https://github.com/scalar/scalar) is the interactive API client (manual testing). It is **not** required for automated checks.

With services running:

- `/openapi/v1.json`, `/openapi/v0.json` — OpenAPI document per API version (Quotes; Auth serves `v1` only)
- `/scalar` — Scalar UI per API
- Docs combined: `http://localhost:3001/scalar/`
- Aspire dashboard (run mode): **Scalar** links on `auth-api` / `quotes-api` (per-service UI) and on `docs` (combined Auth+Quotes reference at `/scalar/`)

Static YAML: `docs/openapi/auth.openapi.yaml`, `docs/openapi/quotes-v0.openapi.yaml`, `docs/openapi/quotes-v1.openapi.yaml`. Refresh with `./scripts/update-contracts.sh` (Podman/Docker via [`Dockerfile.build`](Dockerfile.build)) after Api/DTO changes — see [docs/api.md](docs/api.md) (stub: [contracts/api-contracts.md](contracts/api-contracts.md)).

## Observability

- **Serilog** → console + OTLP (Aspire structured logs), enriched with `CorrelationId`
- **Traces** → ASP.NET + HttpClient instrumentation
- **Metrics** (meter `AspireQuotesPoc`): `auth.login.count` (`outcome=success|failure`), `auth.validate.count` (`outcome=success|failure`), `quotes.random.count` (`outcome=success|not_found`), `quotes.getbyid.count` (`outcome=success|not_found`), `quotes.list.count` (`outcome=success|invalid`), `quotes.create.count` (`outcome=success|invalid|conflict|error`)

See [docs/observability.md](docs/observability.md).

## Libraries

- OpenAPI + Scalar.AspNetCore
- Data Annotations + `AddValidation()` (transport guards on request DTOs)
- ErrorOr (ratified error/result standard for Domain and Application)
- Serilog
- Microsoft.AspNetCore.Authentication.JwtBearer (host auth + scope policies)
- OpenTelemetry (ASP.NET, HttpClient, runtime + custom meters)
- ProblemDetails / health checks
- Reqnroll + Aspire.Hosting.Testing (`tests/Bdd` specs against the real stack)
- Playwright + playwright-bdd (`frontend/e2e` browser journeys)

## Credentials and secrets

- Maintainer (read + write): `jrb` / `supersecret`
- Reader (read only): `reader` / `readsecret`
- JWT signing key: **not committed**. For standalone `dotnet run` in Development, put the documented dev key in user-secrets (Aspire `run` injects the shared `jwt-signing-key` parameter automatically):

```bash
dotnet user-secrets set "Jwt:SigningKey" "AspireQuotesPoc-Dev-Signing-Key-32chars!" --project src/Auth/Auth.Api
dotnet user-secrets set "Jwt:SigningKey" "AspireQuotesPoc-Dev-Signing-Key-32chars!" --project src/Quotes/Quotes.Api
```

Production startup fails if the key is missing or equal to the public development key (`JwtAuthExtensions`). The hermetic OpenAPI export (`Dockerfile.build`) uses a build-time ephemeral key.
