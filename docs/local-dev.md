# Local development

## Prerequisites

- .NET 10 SDK
- Aspire CLI 13.x
- Podman (`ASPIRE_CONTAINER_RUNTIME=podman`)
- Node.js 20.19+ / 22.13+ / 24+

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

## API smoke (curl)

```bash
./scripts/test-api.sh
```

Does not require Scalar. Optional: `AUTH_URL` / `QUOTES_URL` overrides.

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
cd frontend && npm test
# or: npm run test:coverage
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
