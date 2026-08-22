# Testing

Unit tests cover the Auth and Quotes layers plus ServiceDefaults helpers. Frontend coverage is emitted as LCOV for SonarQube.

## Stack

| Area | Tools |
|------|--------|
| .NET | xUnit v3, NSubstitute, Shouldly, Coverlet (OpenCover) |
| Frontend | Vitest, Testing Library, `@vitest/coverage-v8` (LCOV) |

## Layout

```text
tests/
  Auth/
    Auth.Application.Tests/
    Auth.Infrastructure.Tests/
    Auth.Api.Tests/
  Quotes/
    Quotes.Domain.Tests/
    Quotes.Application.Tests/
    Quotes.Infrastructure.Tests/
    Quotes.Api.Tests/
  ServiceDefaults.Tests/
  coverlet.runsettings
frontend/src/**/*.test.ts(x)
```

## Run .NET tests

```bash
./scripts/test.sh
```

Uses `tests/coverlet.runsettings` (OpenCover). Extra `dotnet test` args can be appended:

```bash
./scripts/test.sh --filter FullyQualifiedName~AuthService
```

## Run frontend tests

```bash
cd frontend
npm test                 # vitest run
npm run test:coverage    # + LCOV under frontend/coverage/
```

## What is covered

- **Auth.Application** — login success/failure (ErrorOr), blank input, validate delegation
- **Auth.Infrastructure** — JWT round-trip, expiry, issuer/audience/key mismatch, scope claims, hardcoded credentials
- **Auth.Api** — login/validate handlers incl. the 401 ProblemDetails shape
- **Quotes.Domain** — `Quote.Create` catalog rules (all error codes), fingerprint normalization, `Reconstitute` guards
- **Quotes.Application** — random (incl. empty-catalog 404 path), get-by-id, create success/invalid/conflict
- **Quotes.Infrastructure** — repository contract suite (`QuoteRepositoryContractTests`, inherited by any future adapter), seeded catalog behavior, deterministic `IQuoteSelector`, DI wiring
- **Quotes.Api** — handler-level units (200/201/400/404/409), JWT integration (401 problem + `WWW-Authenticate`, 403 without `quotes:write`), and a **full-pipeline `WebApplicationFactory<Program>` suite** booting the real composition root (create → Location → GET round trip, duplicate 409, validation 400, domain 400)
- **ServiceDefaults** — correlation middleware, metrics (all four counters), `ValidationEndpointFilter` (incl. fail-closed on a missing validator), ErrorOr→ProblemDetails mapping, dev-key Production guard, host wiring (health/OpenAPI/Scalar in every environment)
- **Frontend** — `api/client`, `LoginPage`, `QuotePage`, routing/`RequireAuth`

## CI

`.github/workflows/ci.yml` runs the test suite in **Release** (where `TreatWarningsAsErrors` applies) and regenerates the OpenAPI contracts hermetically, failing on any drift vs `docs/openapi/`.

## Smoke (running stack)

With Aspire up:

```bash
./scripts/test-api.sh
```
