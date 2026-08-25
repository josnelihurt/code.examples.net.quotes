# Testing

Four layers, each owning exactly one question. The bottom two are TDD — exhaustive and
fast; the top two are BDD — few scenarios in business language, run against real
processes. Frontend coverage is emitted as LCOV for SonarQube.

## The testing model

| Layer | Lives in | Proves | Style |
|---|---|---|---|
| Domain / Application units | `tests/{Auth,Quotes}/*.{Domain,Application}.Tests` | Invariants, error codes, paging arithmetic, ErrorOr branches | TDD, exhaustive, microseconds |
| API pipeline tests | `tests/{Auth,Quotes}/*.Api.Tests` | Transport mapping, status codes, ProblemDetails shape, `Location`, `WWW-Authenticate`, v0/v1 parity, OpenAPI parity | TDD, exhaustive per endpoint, in-process |
| Specs | `tests/Bdd` | Journeys that cross a process boundary, in business language | BDD, few, out-of-process (the real Aspire stack) |
| E2E | `frontend/e2e` | What a human does in a browser | BDD, fewest, real Chromium |

**The rule that keeps the suite from doubling in size:**

> If it can be proven without leaving one process, it does not belong in Gherkin.

Every one of the ~10 `quote.text_*` / `quote.author_*` validation permutations stays a
`Quotes.Domain.Tests` fact; Gherkin gets *one* scenario proving that a rejected quote
surfaces as a 400 problem to a caller who came through the gateway. Same for the 429
shape, the two different 400 body shapes, and the telemetry decorators — all already
covered in-process.

## The developer loop (outside-in, TDD inside BDD)

1. Write the scenario in `tests/Bdd/Features/…` with the stakeholder. It fails — no step
   bindings yet. *(red, outer)*
2. Drop to the inner loop: unit-test the domain rule → green; unit-test the endpoint
   mapping → green. *(red/green, inner)*
3. Implement the step definitions. The scenario goes green. *(green, outer)*
4. If the HTTP surface changed, `./scripts/update-contracts.sh` and commit the
   regenerated `docs/openapi/*.yaml`.

## Stack

| Area | Tools |
|------|--------|
| .NET units + API pipelines | xUnit v3, NSubstitute, Shouldly, Coverlet (OpenCover) |
| Specs (tests/Bdd) | Reqnroll (Gherkin) + `Aspire.Hosting.Testing`, xUnit v3, Shouldly |
| Frontend units | Vitest, Testing Library, `@vitest/coverage-v8` (LCOV) |
| Frontend E2E | Playwright + playwright-bdd (Chromium) |

## Layout

```text
tests/
  Auth/            (Application, Infrastructure, Api tests)
  Quotes/          (Domain, Application, Infrastructure, Api tests)
  Architecture.Tests/   (NetArchTest layering rules)
  ServiceDefaults.Tests/
  Bdd/             Reqnroll specs against the running Aspire stack
    Features/      Authentication/ Quotes/ Platform/
    Steps/         Step definitions, split by vocabulary
    Support/       AspireStack (boot once per run), ApiWorld (per-scenario state)
  coverlet.runsettings
frontend/
  src/**/*.test.ts(x)      Vitest units
  e2e/                     Playwright BDD (features/ + steps/)
```

## Run .NET tests (inner loop)

```bash
./scripts/test.sh
```

Uses `tests/coverlet.runsettings` (OpenCover) and the same per-project `*.Tests.csproj`
glob CI uses — which is why the spec suite is *not* swept in: it boots the whole Aspire
model and takes minutes. The Quotes Infrastructure/Api suites now need a container
runtime too (Testcontainers starts a throwaway PostgreSQL per run; `scripts/env.sh`
points the Docker API at the podman machine socket when needed). Extra `dotnet test`
args can be appended:

```bash
./scripts/test.sh --filter FullyQualifiedName~AuthService
```

## Run the specs

```bash
./scripts/bdd.sh
```

Boots the real AppHost per test run (`Aspire.Hosting.Testing`): `auth-api` and
`quotes-api` as separate processes plus the YARP `gateway` container — YARP routing and
service discovery are exercised, not stubbed. Requires Podman locally (via
`scripts/env.sh`) or Docker in CI. The SPA and docsify resources are removed from the
model before startup; browser coverage lives in `frontend/e2e`. The auth rate limit is
raised for the spec environment (many scenarios sign in inside one fixed window); the
429 shape itself is proven by `AuthRateLimitTests`.

~20 scenarios across Authentication, Quotes and Platform features. Expect the first run
to take a few minutes (container start dominates).

## Run frontend tests and E2E

```bash
cd frontend
pnpm test                 # vitest run
pnpm run test:coverage    # + LCOV under frontend/coverage/

./scripts/e2e.sh         # from the repo root: builds APIs (Release), runs Playwright BDD
```

The E2E suite boots both APIs on loopback ports namespaced per worktree (see
[Concurrent worktrees](#concurrent-worktrees)) plus the Vite dev server via
Playwright's `webServer`, then drives the real UI in Chromium: sign in, wrong
credentials, the unauthenticated redirect, random quote, switching transport version,
sign-out; browsing and paging the catalog; publishing a quote — including the
rule-breaking 400, the near-duplicate 409 and the read-only account's 403. Scenarios
sign in through the UI every time — signing in is one of the flows under test. The
auth rate limit is raised for the E2E environment exactly as the spec suite does
(many scenarios sign in inside one fixed window). Feature files deliberately reuse the
spec suite's business vocabulary (`I have published a quote with unique text
attributed to …` mirrors `tests/Bdd/Features/Quotes/PublishingQuotes.feature`): one
language for the API journeys in Reqnroll, one for the browser journeys in
playwright-bdd. The suite runs with `workers: 1` — every scenario of a run shares the
throwaway PostgreSQL catalog that `scripts/e2e.sh` starts (the API migrates + seeds it
at boot), and browsing scenarios assert exact page counts over the seeded catalog.

## Concurrent worktrees

Several checkouts of this repo — multiple agents on one machine sharing one container
runtime — can run the suites at the same time. Nothing needs to be configured: names
and ports are namespaced per worktree from an 8-hex hash of the repo root, stable
inside a checkout and unique across checkouts.

- `scripts/e2e.sh` names its throwaway PostgreSQL `aspirequotes-e2e-pg-<hash>`,
  publishes it on a runtime-assigned ephemeral host port (read back and handed to the
  APIs), and derives the API/Vite ports from the same hash, base 23000–23799. That
  range sits below the OS ephemeral range (Linux 32768+, macOS 49152+ — the kernel
  otherwise leases those ports to outgoing connections; the Notes app broke a run that
  way once) and clear of this repo's own fixed ports (Aspire dashboard/OTLP
  15142–22035, docs 3001, Sonar 9000, PostgreSQL 55432). Playwright receives the ports
  through `E2E_AUTH_PORT` / `E2E_QUOTES_PORT` / `E2E_VITE_PORT` / `E2E_PG_PORT`; each
  is overridable, and `frontend/playwright.config.ts` falls back to the historical
  fixed API/Vite ports — and, for the catalog, to `scripts/e2e.env`, the one copy of
  the throwaway connection values that CI also starts its own PostgreSQL from.
- `scripts/update-contracts.sh` tags its export image `…:export-<hash>`, so two
  worktrees exporting contracts never race the same image tag.
- `scripts/start.sh` passes `--isolated` to `aspire run`: randomized dashboard/service
  ports and a per-run user-secrets store, so several AppHost sessions can coexist.
- `scripts/bdd.sh` and `scripts/test.sh` needed no change: the Aspire testing builder
  randomizes ports by default, and Testcontainers already picks random host ports and
  per-test databases.

Still machine-global by design — serialize these across worktrees: the Sonar scripts
(`sonar-up.sh` / `sonar-down.sh`: one fixed container, volumes and port 9000 hold the
server state) and `scripts/export-bundle.sh` (writes `~/repo.bundle`).

## What is covered

- **Auth.Application** — login success/failure (ErrorOr), blank input, validate delegation
- **Auth.Infrastructure** — JWT round-trip, expiry, issuer/audience/key mismatch, scope claims, hardcoded credentials
- **Auth.Api** — login/validate handlers incl. the 401 ProblemDetails shape
- **Quotes.Domain** — `QuoteText` / `QuoteAuthor` / `QuoteFingerprint` value objects, `Quote.Create` composition (incl. `AuthorEqualsText`), `Reconstitute` / `FromTrusted` guards
- **Quotes.Application** — random (incl. empty-catalog 404 path), get-by-id, list paging arithmetic and range validation, create success/invalid/conflict
- **Quotes.Infrastructure** — repository contract suite (`QuoteRepositoryContractTests`, inherited by any future adapter; covers list paging, no-overlap, beyond-end) run against real PostgreSQL via Testcontainers (one database per test), the migration-shipped seed catalog (ids `"1"`–`"8"`), DI wiring
- **Quotes.Api** — handler-level units (200/201/400/404/409), JWT integration (401 problem + `WWW-Authenticate`, 403 without `quotes:write`), and a **full-pipeline `WebApplicationFactory<Program>` suite** booting the real composition root (list pages + defaults, create → Location → GET round trip, duplicate 409, validation 400, domain 400)
- **ServiceDefaults** — correlation middleware, metrics (all six counters), ErrorOr→ProblemDetails mapping, dev-key Production guard, host wiring (health/OpenAPI/Scalar in every environment)
- **Architecture** — NetArchTest suite (`tests/Architecture.Tests`) enforcing the layering table: dependency direction per layer, no Api→Domain, no cross-context references, ServiceDefaults isolated
- **Auth rate limiting** — slim-pipeline suite with a two-request window proving the 429 ProblemDetails shape (`auth.rate_limited`), plus the Production refusal of the scaffolding credential store at the DI boundary
- **Specs (tests/Bdd)** — cross-service journeys through the gateway: sign in → token → random quote, create → `Location` round trip, near-duplicate 409, rejected text 400, reader-scope 403, v0/v1 transport parity, token introspection, OpenAPI/Scalar surfaces
- **Frontend** — `api/client` (session, login, random, catalog paging, publish — every failure path parsed out of the RFC 9457 body into `ApiError`), `LoginPage`, `QuotePage`, `QuotesListPage` (first page, next/previous bounds, version switch refetch, empty catalog), `PublishQuotePage` (success confirmation + form reset, validation/conflict/forbidden alerts, in-flight disabling), routing/`RequireAuth` over `/quote`, `/quotes` and `/publish` (Vitest); browser journeys across signing-in, reading-quotes, browsing-quotes and publishing-quotes (Playwright BDD); Storybook interaction stories for the extracted presentational components, smoke-built in CI

## CI

`.github/workflows/ci.yml` enforces seven gates: the test suite in **Release** (where
`TreatWarningsAsErrors` applies) with coverage collection; the repo's own lint script
(`dotnet format --verify-no-changes`); the frontend job (lint + tests + build so type
errors cannot pass, plus the Storybook build and a regeneration of the SPA's
OpenAPI-derived types in `src/api/schema.d.ts` failing on drift); the **specs** job
(Reqnroll against the Aspire-orchestrated stack, Docker on `ubuntu-latest`); the
**e2e** job (Playwright + playwright-bdd in Chromium, its throwaway catalog started from
`scripts/e2e.env`); the hermetic OpenAPI
contract regeneration failing on any drift vs `docs/openapi/`; and the **image-pins**
gate (`scripts/check-image-tags.sh`) failing when the container image tags in
`scripts/images.env` drift from the ones the pinned Aspire packages run.
CI Release is the canonical gate; the local `./scripts/test.sh` (Debug + coverage) is
the fast inner loop, and `./scripts/bdd.sh` / `./scripts/e2e.sh` run the slow outer
loops on demand. The old curl-based `smoke` job (and `scripts/test-api.sh`) was replaced
by `specs` and `e2e`: every assertion it made has a home in `tests/Bdd/Features/`.
