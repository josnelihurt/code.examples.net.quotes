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
- **Quotes.Domain** — `QuoteText` / `QuoteAuthor` / `QuoteFingerprint` value objects, `Quote.Create` composition (incl. `AuthorEqualsText`), `Reconstitute` / `FromTrusted` guards
- **Quotes.Application** — random (incl. empty-catalog 404 path), get-by-id, list paging arithmetic and range validation, create success/invalid/conflict
- **Quotes.Infrastructure** — repository contract suite (`QuoteRepositoryContractTests`, inherited by any future adapter; covers list paging, no-overlap, beyond-end), seeded catalog behavior, deterministic `IQuoteSelector`, DI wiring
- **Quotes.Api** — handler-level units (200/201/400/404/409), JWT integration (401 problem + `WWW-Authenticate`, 403 without `quotes:write`), and a **full-pipeline `WebApplicationFactory<Program>` suite** booting the real composition root (list pages + defaults, create → Location → GET round trip, duplicate 409, validation 400, domain 400)
- **ServiceDefaults** — correlation middleware, metrics (all six counters), ErrorOr→ProblemDetails mapping, dev-key Production guard, host wiring (health/OpenAPI/Scalar in every environment)
- **Architecture** — NetArchTest suite (`tests/Architecture.Tests`) enforcing the layering table: dependency direction per layer, no Api→Domain, no cross-context references, ServiceDefaults isolated
- **Auth rate limiting** — slim-pipeline suite with a two-request window proving the 429 ProblemDetails shape (`auth.rate_limited`), plus the Production refusal of the scaffolding credential store at the DI boundary
- **Frontend** — `api/client`, `LoginPage`, `QuotePage`, routing/`RequireAuth`

## CI

`.github/workflows/ci.yml` enforces four gates: the test suite in **Release** (where `TreatWarningsAsErrors` applies) with coverage collection, the repo's own lint script (`dotnet format --verify-no-changes`), a **smoke job** that boots both APIs and runs `scripts/test-api.sh` end to end (login, create round trip, 409/400 negatives, reader-scope 403, list page, OpenAPI/Scalar), and the hermetic OpenAPI contract regeneration failing on any drift vs `docs/openapi/`. The frontend job additionally runs `npm run build` so type errors cannot pass CI. CI Release is the canonical gate; the local `./scripts/test.sh` (Debug + coverage) is the fast inner loop.

## Smoke (running stack)

With Aspire up:

```bash
./scripts/test-api.sh
```
