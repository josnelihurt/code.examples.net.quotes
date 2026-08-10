# Aspire Quotes POC

Local-first distributed sample built with **.NET 10**, **Aspire 13**, **React + TypeScript (Vite)**, and **Podman**.

## What it does

1. **Auth API** issues a JWT for hardcoded user `jrb` / `supersecret` and validates tokens.
2. **Quotes API** returns a random quote from an in-memory dictionary after calling Auth to validate the bearer token.
3. **React SPA** logs in, stores token + `X-Correlation-Id`, then fetches quotes through the Vite proxy.
4. **Aspire AppHost** starts everything, wires service discovery, exports OpenTelemetry to the dashboard, and publishes a **YARP** gateway (no Traefik).

```text
UI (Vite) -> Auth / Quotes
Quotes -> Auth (validate) + Polly retry/circuit-breaker
OTEL metrics/logs/traces -> Aspire dashboard
```

## Solution layout

| Path | Purpose |
|------|---------|
| `src/AppHost/` | Aspire orchestration (`AspireQuotesPoc.AppHost`) |
| `src/ServiceDefaults/` | Serilog, OTEL, OpenAPI/Scalar helpers, Polly, correlation |
| `src/Auth/` | Auth DDD + Minimal API |
| `src/Quotes/` | Quotes DDD + Minimal API |
| `frontend/` | React + TS Vite SPA |
| `docs/` | Docsify + combined Scalar reference |
| `contracts/` | Frozen OpenAPI YAML + contract notes |
| `tests/` | xUnit unit/API tests (OpenCover for Sonar) |
| `scripts/` | Env, start, docs, publish, test, Sonar, bundle |

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

Static YAML: `contracts/auth.openapi.yaml`, `contracts/quotes.openapi.yaml` (mirrored under `docs/openapi/`).

## Observability

- **Serilog** → console + OTLP (Aspire structured logs), enriched with `CorrelationId`
- **Traces** → Quotes → Auth validate hop
- **Metrics** (meter `AspireQuotesPoc`): `auth.login.count`, `auth.validate.count`, `quotes.random.count` with tag `outcome=success|failure`

See [docs/observability.md](docs/observability.md).

## Libraries

- OpenAPI + Scalar.AspNetCore
- FluentValidation (Auth login)
- Serilog
- Microsoft.Extensions.Http.Resilience (Polly v8) on Quotes→Auth
- OpenTelemetry (ASP.NET, HttpClient, runtime + custom meters)
- ProblemDetails / health checks

## Credentials

- User: `jrb`
- Password: `supersecret`
