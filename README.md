# Aspire Quotes

Local-first distributed foundation built with **.NET 10**, **Aspire 13**, **React + TypeScript (Vite)**, and **Podman**.

## Purpose

This repository is the **base template for production microservices** in a large-organization setting—not a disposable demo and not training material for newcomers. The team uses it to align on Clean Architecture, shared platform defaults, and habits that scale to many services and many endpoints.

**Goals**

- **Clear separations** — Domain and Application stay free of HTTP, auth headers, and status codes; Infrastructure owns adapters; Api owns transport, mapping, and OpenAPI.
- **Shared platform** — Cross-cutting concerns that every service needs (correlation, Serilog/OTEL, OpenAPI/Scalar conventions, resilience helpers, ProblemDetails) live in `ServiceDefaults` so they are not reimplemented per endpoint or per service.
- **Low per-endpoint cost** — Auth, error shaping, metrics, and API documentation should be inherited from conventions/filters/handlers; a new endpoint should mostly map a route, call an application use case, and map the result.
- **Contracts as product** — Api DTOs and OpenAPI are the public surface other teams consume. Prefer org-wide error shapes and security metadata over ad-hoc response types per service.
- **Cloneable service shape** — Auth and Quotes show the same layering (`Api` / `Application` / `Domain` / `Infrastructure`) so the next microservice can copy the structure without inventing policy.

**Layering (dependency rule)**

| Tier | Owns | Must not own |
|------|------|--------------|
| **Platform** (`ServiceDefaults`) | Auth integration patterns, correlation, OpenAPI conventions, telemetry, resilience | Business rules |
| **Service host** (`*.Api`) | Composition root, endpoints, transport DTOs, mapping to/from Application | Persistence details, remote client internals |
| **Application** | Use cases, ports | `HttpContext`, bearer parsing, OpenAPI, status codes |
| **Domain** | Entities and domain ports | HTTP, DI containers, infrastructure SDKs |
| **Infrastructure** | Repositories, HTTP clients, external systems | Endpoint contracts, Swagger UI concerns |

**Domain terms (this repo)**

.NET / DDD vocabulary maps cleanly to Go-style `entities`, but folder names often differ: types usually live under `*.Domain` (sometimes `Domain/Entities/`). Do not confuse domain entities with EF persistence classes.

| Term | Meaning | In this project |
|------|---------|-----------------|
| **Entity** | Domain object with identity and invariants | [`Quotes.Domain.Quote`](src/Quotes/Quotes.Domain/Quote.cs) — created via `Quote.Create`, owns catalog rules and `NormalizedFingerprint` |
| **Value object** | No identity; equality by value; often embedded in an entity | Not a separate type yet; fingerprint/normalized text behave as value concepts inside `Quote`. Prefer extracting (e.g. `QuoteFingerprint`) when reused or rules grow |
| **Aggregate** | Consistency boundary: one root entity that other objects change through | `Quote` is a small aggregate root (single entity). Repositories load/save the root (`IQuoteRepository`), not internal bits |
| **EF / persistence model** | Storage shape (table/document row), mapping, DB concerns | [`QuoteRecord`](src/Quotes/Quotes.Infrastructure/Persistence/QuoteRecord.cs) in Infrastructure. Today in-memory; an EF `DbSet<QuoteRecord>` (or `QuoteEntity`) would stay here — **never** put EF attributes on Domain `Quote` |

**Rule of thumb:** Domain speaks `Quote`; Infrastructure maps `Quote` ↔ `QuoteRecord` at the repository boundary. Api DTOs (`CreateQuoteRequestDto`, `QuoteResponseDto`) are transport only.

**Direction of travel** (foundation backlog reflected in this sample’s evolution)

1. Authentication and authorization at the host/platform — Quotes uses JwtBearer + `RequireAuthorization`; writes require the `quotes:write` scope (see `JwtAuthExtensions.WriteQuotesPolicy`), reads only an authenticated user. Extend the same pattern to new services.
2. Thin Minimal API endpoints plus explicit mappers; Application outcomes are `ErrorOr` results mapped once to RFC 9457 ProblemDetails (`ErrorOrHttpExtensions.ToProblem`), with `errorCode` and `correlationId` as extensions. Expected failures never travel as exceptions.
3. OpenAPI conventions in the platform (Bearer/security scheme via document transformer, standard 401/403/404/409/500 ProblemDetails shapes) so documentation stays consistent as endpoints grow.
4. Composition root at the API host: each layer registers itself (`AddQuotesApplication`, `AddQuotesInfrastructure`), and Program.cs composes them. The Api project references Application + Infrastructure, never Domain directly.
5. Fail-closed input validation: the shared `ValidationEndpointFilter<T>` (ServiceDefaults) refuses to run without a registered `IValidator<T>` instead of silently skipping validation.

Hardcoded credentials and in-memory quotes are **local scaffolding** so the foundation runs offline. They are not a model for production identity or storage.

## What it does today

1. **Auth API** issues a JWT (with `quotes:read` / `quotes:write` scope claims) for hardcoded user `jrb` / `supersecret` and can validate tokens via `/api/auth/validate` (introspection demo).
2. **Quotes API** serves quotes from an in-memory catalog after JwtBearer middleware validates the bearer token locally: `GET /api/quotes/random`, `GET /api/quotes/{id}`, and `POST /api/quotes` (requires the `quotes:write` scope; creates validate catalog rules and reject near-duplicates by fingerprint).
3. **React SPA** logs in, stores token + `X-Correlation-Id`, then fetches quotes through the Vite proxy.
4. **Aspire AppHost** starts everything, wires service discovery, exports OpenTelemetry to the dashboard, and publishes a **YARP** gateway (no Traefik).

```text
UI (Vite) -> Auth / Quotes
Quotes validates JWT locally (JwtBearer); Auth /validate remains for introspection demos
OTEL metrics/logs/traces -> Aspire dashboard
```

## Solution layout

| Path | Purpose |
|------|---------|
| `src/AppHost/` | Aspire orchestration (`AspireQuotesPoc.AppHost`) |
| `src/ServiceDefaults/` | Platform kit: Serilog, OTEL, OpenAPI/Scalar helpers, JwtBearer auth + scope policies, fail-closed validation filter, ErrorOr→ProblemDetails mapper, correlation |
| `src/Auth/` | Auth service — Domain / Application / Infrastructure / Api |
| `src/Quotes/` | Quotes service — Domain / Application / Infrastructure / Api |
| `frontend/` | React + TS Vite SPA |
| `docs/` | Docsify + combined Scalar reference |
| `contracts/` | Pointer to Docsify OpenAPI docs ([api-contracts.md](contracts/api-contracts.md)) |
| `tests/` | xUnit unit/API tests (OpenCover for Sonar) |
| `scripts/` | Env, start, docs, publish, test, update-contracts, Sonar, bundle |

## How to run

```bash
./scripts/start.sh
```

Uses `scripts/env.sh` (`ASPIRE_CONTAINER_RUNTIME=podman`, `ASPNETCORE_ENVIRONMENT=Development`). Open the Aspire dashboard URL from the console, then the `web` endpoint.

Documentation:

```bash
./scripts/serve-docs.sh
```

API smoke (curl, no Scalar required):

```bash
./scripts/test-api.sh
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

Frontend tests (Vitest):

```bash
cd frontend && npm test
```

Local SonarQube (Podman) + scan:

```bash
./scripts/sonar-up.sh
./scripts/sonar-scan.sh
```

Export a full git bundle to `~/repo.bundle`:

```bash
./scripts/export-bundle.sh
```

More detail in Docsify: [Testing](docs/testing.md), [SonarQube](docs/sonar.md).
## OpenAPI / Scalar

[Scalar](https://github.com/scalar/scalar) is the interactive API client (manual testing). It is **not** required for automated checks.

With services running:

- `/openapi/v1.json` — OpenAPI document
- `/scalar` — Scalar UI per API
- Docs combined: `http://localhost:3001/scalar/`
- Aspire dashboard (run mode): **Scalar** links on `auth-api` / `quotes-api` (per-service UI) and on `docs` (combined Auth+Quotes reference at `/scalar/`)

Static YAML: `docs/openapi/auth.openapi.yaml`, `docs/openapi/quotes.openapi.yaml`. Refresh with `./scripts/update-contracts.sh` (Podman/Docker via [`Dockerfile.build`](Dockerfile.build)) after Api/DTO changes — see [docs/api.md](docs/api.md) (stub: [contracts/api-contracts.md](contracts/api-contracts.md)).

## Observability

- **Serilog** → console + OTLP (Aspire structured logs), enriched with `CorrelationId`
- **Traces** → ASP.NET + HttpClient instrumentation
- **Metrics** (meter `AspireQuotesPoc`): `auth.login.count` (`outcome=success|failure`), `auth.validate.count` (`outcome=success|failure`), `quotes.random.count` (`outcome=success|not_found`), `quotes.create.count` (`outcome=success|invalid|conflict|error`)

See [docs/observability.md](docs/observability.md).

## Libraries

- OpenAPI + Scalar.AspNetCore
- FluentValidation (shared `ValidationEndpointFilter<T>` + per-DTO validators)
- ErrorOr (ratified error/result standard for Domain and Application)
- Serilog
- Microsoft.AspNetCore.Authentication.JwtBearer (host auth + scope policies)
- OpenTelemetry (ASP.NET, HttpClient, runtime + custom meters)
- ProblemDetails / health checks

## Credentials and secrets

- User: `jrb`
- Password: `supersecret`
- JWT signing key: **not committed**. For standalone `dotnet run` in Development, put the documented dev key in user-secrets (Aspire `run` injects the shared `jwt-signing-key` parameter automatically):

```bash
dotnet user-secrets set "Jwt:SigningKey" "AspireQuotesPoc-Dev-Signing-Key-32chars!" --project src/Auth/Auth.Api
dotnet user-secrets set "Jwt:SigningKey" "AspireQuotesPoc-Dev-Signing-Key-32chars!" --project src/Quotes/Quotes.Api
```

Production startup fails if the key is missing or equal to the public development key (`JwtAuthExtensions`). The hermetic OpenAPI export (`Dockerfile.build`) uses a build-time throwaway key.
