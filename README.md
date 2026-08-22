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

**Direction of travel** (foundation backlog reflected in this sample’s evolution)

1. Authentication and authorization at the host/platform — Quotes uses JwtBearer + `RequireAuthorization` on `/api/quotes` (extend the same pattern to new services).
2. Thin Minimal API endpoints plus explicit mappers; Application outcomes mapped once to ProblemDetails (or a single org error contract).
3. OpenAPI conventions in the platform (Bearer/security scheme, standard 401/403/500 shapes) so documentation stays consistent as endpoints grow.
4. Split `AddApplication` / `AddInfrastructure` at the composition root; keep MediatR (or similar) as an optional later standard once cross-cutting behaviors justify it—not required for the first vertical slices.

Hardcoded credentials and in-memory quotes are **local scaffolding** so the foundation runs offline. They are not a model for production identity or storage.

## What it does today

1. **Auth API** issues a JWT for hardcoded user `jrb` / `supersecret` and can validate tokens via `/api/auth/validate` (introspection demo).
2. **Quotes API** returns a random quote from an in-memory dictionary after JwtBearer middleware validates the bearer token locally.
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
| `src/ServiceDefaults/` | Platform kit: Serilog, OTEL, OpenAPI/Scalar helpers, JwtBearer auth, Polly, correlation |
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
- **Metrics** (meter `AspireQuotesPoc`): `auth.login.count`, `auth.validate.count`, `quotes.random.count` with tag `outcome=success|failure`

See [docs/observability.md](docs/observability.md).

## Libraries

- OpenAPI + Scalar.AspNetCore
- FluentValidation (Auth login)
- Serilog
- Microsoft.AspNetCore.Authentication.JwtBearer (Quotes host auth)
- Microsoft.Extensions.Http.Resilience (Polly v8 helpers in ServiceDefaults)
- OpenTelemetry (ASP.NET, HttpClient, runtime + custom meters)
- ProblemDetails / health checks

## Credentials

- User: `jrb`
- Password: `supersecret`
