# Local development

## Prerequisites

- .NET 10 SDK
- Aspire CLI 13.x
- A checkout with the frontend submodule present: `git clone --recurse-submodules …`
  (or `git submodule update --init` in an existing clone) — the AppHost's `web`
  resource and the e2e suite resolve `frontend/` from it
- Podman (`ASPIRE_CONTAINER_RUNTIME=podman`)
- Node.js 20.19+ / 22.12+ / 24+ (CI runs Node 24, the active LTS)
- pnpm — install standalone (`brew install pnpm` or `npm i -g pnpm`); Corepack no longer
  ships with Node 25+. The exact version is pinned by `packageManager` in
  `frontend/package.json` (inside the submodule) and pnpm honors it automatically.
  Rationale: [pnpm as the package manager](https://github.com/josnelihurt/code.examples.frontend.quotes/blob/main/docs/package-manager-security.md) (moved to the frontend repository with the SPA)

## Start the app

```bash
./scripts/start.sh
```

This sources `scripts/env.sh` and runs `aspire run` (AppHost at `src/AppHost/` via `aspire.config.json`).

## Docs only

```bash
./scripts/serve-docs.sh
```

Opens Docsify on port **3001** (combined Scalar at `/scalar/`).

## Verify the documentation

```bash
./scripts/verify-docs.sh                 # links, code references, mermaid
./scripts/verify-docs.sh --skip-mermaid  # skip the pnpm/Chromium render pass
```

Checks that every markdown link and anchor resolves, that every repo path, route and identifier the
component pages cite exists in the code, and that every mermaid diagram renders. Details:
[Documentation process](documentation-process.md).

## API specs (against the running stack)

```bash
./scripts/bdd.sh
```

Reqnroll journeys through the YARP gateway (login, random, create + Location round trip,
409 duplicate, 400 invalid, reader-scope 403). Does not require Scalar; needs Podman.

## Scalar (interactive)

```bash
./scripts/open-scalar.sh
```

## Publish Compose artifacts

```bash
./scripts/publish.sh
```

Output: `src/AppHost/aspire-output/`

## Unit tests

```bash
./scripts/test.sh
```

Frontend:

```bash
cd frontend && pnpm install   # first time only
pnpm test
# or: pnpm run test:coverage
```

Details: [Testing](testing.md).

## SonarQube (Podman)

```bash
./scripts/sonar-up.sh
./scripts/sonar-scan.sh
```

Details: [SonarQube](sonar.md).

## Export a git bundle

Creates (or replaces) `~/repo.bundle` for taking the repo offline:

```bash
./scripts/export-bundle.sh
```
