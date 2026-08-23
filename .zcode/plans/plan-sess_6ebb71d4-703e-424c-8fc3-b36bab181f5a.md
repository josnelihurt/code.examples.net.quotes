# Create & List Quotes UI — full test pyramid, OpenAPI typegen, shared Gherkin vocabulary, Storybook + CI

Branch: `feature/web-app-add-quotes-list-quotes` (already checked out, clean).

## Current state (from background agents' evaluation)

- Backend already exposes everything needed on **both** transports (v0 controllers / v1 minimal APIs): `GET /api/{v0,v1}/quotes?page=&pageSize=` (200 `QuotePageResponseDto`, 400 `quote.invalid_page_request`; page ≥ 1, pageSize 1–100 default 20), `POST /api/{v0,v1}/quotes` (201 + `Location`, 400 domain-rule problems, 409 `quote.duplicate_fingerprint`, 403 without `quotes:write`). In-memory singleton repository seeds 8 quotes; created quotes appear immediately in list.
- SPA (React 19 + Vite + react-router 7) only calls `login` and `getRandomQuote` (`frontend/src/api/client.ts`). Pages: `LoginPage` (`/`), `QuotePage` (`/quote`). No components folder, no Storybook, no typegen.
- Tests: Vitest+Testing Library units colocated in `frontend/src`; playwright-bdd E2E in `frontend/e2e` (features `signing-in`, `reading-quotes`; steps in `e2e/steps`); Reqnroll specs in `tests/Bdd` already cover API-level create/list journeys — **no new Reqnroll scenarios needed** (repo rule: if it can't be proven in one process it goes to Gherkin; API journeys are already there).
- Latent flake found: `frontend/playwright.config.ts` does **not** raise the auth login rate limit (10/30s per IP) while every E2E scenario signs in via the UI; adding ~7 more sign-ins would trip 429. The spec suite already solves this with `RateLimiting__Auth__PermitLimit`.

## Phase 1 — API client (`frontend/src/api/client.ts`)

1. Introduce `ApiError extends Error { status, errorCode?, title? }` parsing the RFC 9457 ProblemDetails body; refactor `login`/`getRandomQuote` to throw it while **preserving existing message formats** (`${reason} (${status})` for login, `Quote request failed (${status})` for random) so current unit tests/E2E stay green.
2. Extract a shared authed-fetch helper (Bearer + `X-Correlation-Id` + `Not authenticated` guard).
3. Add `QuotePageResponse { items, page, pageSize, totalItems, totalPages }`, `listQuotes({ page?, pageSize? }, version?)` and `createQuote({ text, author }, version?)` returning the created `QuoteResponse` (Location round-trip stays covered at spec level).

## Phase 2 — OpenAPI typegen (specs ↔ UI integration)

1. Add `openapi-typescript` devDep; script `gen:api` generating `src/api/schema.d.ts` from the frozen `docs/openapi/quotes-v1.openapi.yaml` (v1 canonical; both transports are shape-identical).
2. Derive the client's `QuoteResponse`/`QuotePageResponse`/create-request types from the generated schema instead of hand-written interfaces.
3. Wire `npm run gen:api` into `scripts/update-contracts.sh` and add a drift check (`npm run gen:api && git diff --exit-code src/api/schema.d.ts`) to the CI `frontend` job.

## Phase 3 — UI: three routes with shared nav

1. Extract presentational components to `frontend/src/components/`: `VersionSwitcher` (extracted from `QuotePage`, **keeping radio ids `#version-v0/v1`** — an E2E step depends on them), `ErrorAlert`, `QuoteCard`, `Pager`. Refactor `QuotePage` to use them with zero behavior change (headings/selectors preserved: `Random quote`, `blockquote.quote`, `Sign out`).
2. `App.tsx`: add `/quotes` and `/publish` behind `RequireAuth` (via an authenticated layout route with `<Outlet/>`); nav "Random · Browse · Publish" on all three pages; `/quote` untouched.
3. `pages/QuotesListPage.tsx` (`/quotes`): fetch on mount, **UI default `pageSize: 5`** so paging is visible over the 8 seeds; Previous/Next with disabled bounds, "Page x of y · N quotes"; loading/error/empty states; `Served by` indicator + version switcher (version persists via sessionStorage as today).
4. `pages/PublishQuotePage.tsx` (`/publish`): Text textarea (`maxLength 280`) + Author input (`maxLength 80`, mirroring contract metadata; server remains the single source of validation); success panel showing the created quote with a link to `/quotes`; error alerts for 400 problem title, 409 conflict, 403 forbidden (reader account).
5. `App.css`: nav, list/card, form styles matching the existing IBM Plex/Fraunces look.

## Phase 4 — Frontend unit tests (Vitest + Testing Library, colocated)

1. `api/client.test.ts`: `listQuotes` (default query omits params, explicit page/pageSize serialized, parses page response, 400 → `ApiError` with `quote.invalid_page_request`); `createQuote` (201 returns body; 400/409/403 problem → `ApiError` with status+errorCode; unauthenticated guard); ApiError message-format regression for login/random.
2. `pages/QuotesListPage.test.tsx`: renders seeded cards; Next requests page 2; Previous disabled on page 1; fetch failure shows alert; empty catalog state.
3. `pages/PublishQuotePage.test.tsx`: success shows created quote; 400 surfaces problem title; 409 and 403 alerts; loading disables submit.
4. `App.test.tsx`: `/quotes` and `/publish` redirect when unauthenticated; nav links render when signed in.

## Phase 5 — E2E: new playwright-bdd features (integration automation from the frontend)

1. `playwright.config.ts`: add `RateLimiting__Auth__PermitLimit: '100'` to the Auth API `webServer` env (mirroring `tests/Bdd/Support/AspireStack.cs`) — fixes the 429 flake before adding scenarios.
2. `e2e/features/browsing-quotes.feature` — vocabulary aligned with `tests/Bdd/Features/Quotes/BrowsingQuotes.feature`; Background signs in as `jrb` via the UI:
   - The first page of the catalog lists seeded quotes (known card visible, "Page 1 of 2").
   - Paging moves through the catalog (Next → "Page 2 of 2", Previous → back).
   - The v0 transport serves the catalog (switch version → "Served by: v0").
3. `e2e/features/publishing-quotes.feature` — aligned with `PublishingQuotes`/`Authorization`:
   - A maintainer publishes a new quote (unique timestamped text, ≥3 words, terminal punctuation; author "E2E Suite") → confirmation shows it, and it appears when browsing.
   - Text that breaks the catalog rules is explained inline ("short" → alert).
   - A near-duplicate is rejected as a conflict (same text twice → 409 alert).
   - A reader cannot publish (sign in as `reader`/`readsecret` → 403 alert).
4. Steps in new `e2e/steps/browsing.steps.ts` + `e2e/steps/publishing.steps.ts` (repo splits steps by vocabulary); quote uniqueness via `Date.now()` avoids intra-run 409s (APIs boot fresh per E2E run, so cross-run state is impossible).

## Phase 6 — Storybook 9 + stories + CI smoke

1. DevDeps: `storybook@^9`, `@storybook/react-vite`, `@storybook/test`, `@storybook/addon-a11y`.
2. `.storybook/main.ts` (react-vite framework, stories glob `../src/**/*.stories.tsx`) + `.storybook/preview.ts` (import existing global CSS).
3. Stories: `QuoteCard`, `Pager` (first/middle/last), `ErrorAlert`, `VersionSwitcher`, and interaction stories for the publish form and list states (empty/error) via `@storybook/test` with prop/spy-level mocking — no extra fetch-mocking dependency.
4. Scripts `storybook` / `build-storybook`; exclude `*.stories.tsx` from Vitest coverage.
5. CI `frontend` job: add `npm run build-storybook` as the smoke gate.

## Phase 7 — Docs

- `docs/testing.md`: extend the Frontend row ("What is covered") with browse/publish journeys, typegen drift gate, Storybook smoke; document the shared Gherkin vocabulary convention between `tests/Bdd` and `frontend/e2e`.
- `frontend/README.md`: new pages, `gen:api`, Storybook scripts.

## Execution order & verification

Features first (red) → client + typegen → components/pages → unit tests → E2E steps (green) → Storybook → docs/CI. Verify with:
- `cd frontend && npm test && npm run lint && npm run build && npm run build-storybook`
- `./scripts/e2e.sh` (builds APIs, runs the full BDD suite in Chromium)
- Backend untouched; `./scripts/test.sh` unaffected.

## Risks / invariants

- Existing E2E selectors must survive the `QuotePage` refactor: headings `Random quote`/`Sign in`, `#version-v0` radio, `Sign out` button, `blockquote.quote`.
- Login/random error message formats preserved (unit tests assert them).
- `@types/react-router-dom@5` is a pre-existing oddity — left alone.