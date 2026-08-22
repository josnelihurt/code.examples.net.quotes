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
    Quotes.Application.Tests/
    Quotes.Infrastructure.Tests/
    Quotes.Api.Tests/
  ServiceDefaults.Tests/
  coverlet.runsettings
frontend/src/**/*.test.ts(x)
```

Domain projects hold only models/interfaces, so they have no dedicated test assemblies.

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

- **Auth.Application** — login success/failure, blank input, validate delegation
- **Auth.Infrastructure** — JWT round-trip, expiry, issuer/audience/key mismatch, hardcoded credentials
- **Auth.Api** — FluentValidation, `ValidationFilter`, extracted login/validate handlers
- **Quotes.Application** — `GetRandomQuoteUseCase` returns a quote from the repository
- **Quotes.Infrastructure** — in-memory repository, deterministic `IQuoteSelector`, DI wiring
- **Quotes.Api** — thin handler mapping; JwtBearer integration tests for `/api/quotes/random`
- **ServiceDefaults** — correlation middleware, metrics, Polly retries, JwtBearer registration, host wiring (health/OpenAPI/Scalar)
- **Frontend** — `api/client`, `LoginPage`, `QuotePage`, routing/`RequireAuth`

## Smoke (running stack)

With Aspire up:

```bash
./scripts/test-api.sh
```
