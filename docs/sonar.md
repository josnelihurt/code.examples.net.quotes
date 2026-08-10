# SonarQube (local)

Static analysis runs against a **Podman** SonarQube Community Build. The scanner is the host-side `dotnet-sonarscanner` tool (C# cannot be scanned from the generic scanner container alone).

## Prerequisites

- Podman machine with enough RAM (script bumps to **6 GB** if below 4 GB, with a prompt)
- `curl`, `python3`
- Node modules under `frontend/` for TypeScript coverage (`npm install`)

## Start the server

```bash
./scripts/sonar-up.sh
```

Non-interactive resize:

```bash
SONAR_ASSUME_YES=1 ./scripts/sonar-up.sh
```

What it does:

1. Checks / optionally resizes the Podman machine memory
2. Starts container `aspirequotes-sonarqube` on port **9000**
3. Waits until `/api/system/status` is `UP`
4. Rotates the default `admin` password
5. Creates project `aspire-quotes-poc` and writes an analysis token to `.sonar/token`

Defaults (override via env; see `scripts/sonar-env.sh`):

| Variable | Default |
|----------|---------|
| `SONAR_HOST_URL` | `http://localhost:9000` |
| `SONAR_ADMIN_PASSWORD` | `AspireQuotes-Poc1!` |
| `SONAR_PROJECT_KEY` | `aspire-quotes-poc` |

UI: [http://localhost:9000](http://localhost:9000) — login `admin` / password above.

## Run an analysis

```bash
./scripts/sonar-scan.sh
```

Pipeline: frontend Vitest + LCOV → `sonarscanner begin` → `dotnet build` → `dotnet test` (OpenCover) → `sonarscanner end`.

Skip frontend coverage:

```bash
SONAR_SKIP_FRONTEND=1 ./scripts/sonar-scan.sh
```

Dashboard after upload:

`http://localhost:9000/dashboard?id=aspire-quotes-poc`

## Stop / reset

```bash
./scripts/sonar-down.sh           # remove container
./scripts/sonar-down.sh --purge  # also drop volumes + `.sonar/token`
```

## Intentional POC findings

These stay in code with documented suppressions (not production secrets):

- Hardcoded `jrb` / `supersecret` in `HardcodedCredentialStore`
- Aspire service-discovery base address `http://auth-api` (rewritten at runtime)

Real issues (dead code, empty-token guards, duplicated bearer parsing, silent validation, Polly magic numbers) were fixed rather than suppressed.

## Tooling pin

`dotnet-sonarscanner` is pinned in `.config/dotnet-tools.json` (`dotnet tool restore` runs inside `sonar-scan.sh`).
