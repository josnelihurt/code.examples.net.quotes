#!/usr/bin/env bash
# Runs the Playwright BDD suite for the SPA. Builds both APIs in Release first —
# playwright.config.ts points webServer at their DLLs on fixed loopback ports — starts a
# throwaway PostgreSQL for the quotes catalog (the API migrates + seeds it at boot), then
# hands over to Playwright, which boots the APIs and the Vite dev server itself.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"

# Same tag Aspire.Hosting.PostgreSQL pins, so local runs reuse the already-pulled image.
PG_IMAGE="docker.io/library/postgres:18.3"
PG_NAME="aspirequotes-e2e-pg"
PG_PORT="55432"

# Honors ASPIRE_CONTAINER_RUNTIME (podman by default here); both CLIs accept these args.
RUNTIME="${ASPIRE_CONTAINER_RUNTIME:-podman}"

dotnet build "${ROOT}/src/Auth/Auth.Api/Auth.Api.csproj" --configuration Release
dotnet build "${ROOT}/src/Quotes/Quotes.Api/Quotes.Api.csproj" --configuration Release

"${RUNTIME}" pull "${PG_IMAGE}"
"${RUNTIME}" rm -f "${PG_NAME}" >/dev/null 2>&1 || true
"${RUNTIME}" run -d --name "${PG_NAME}" \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=quotesdb \
  -p "127.0.0.1:${PG_PORT}:5432" \
  "${PG_IMAGE}"

# Fresh container per run -> the deterministic seeded catalog the browsing scenarios
# assert on ("8 quotes at 5 per page"). The trap keeps cleanup honest on failures too;
# no exec on the pnpm call, or the trap would never fire.
trap '"${RUNTIME}" rm -f "${PG_NAME}" >/dev/null 2>&1 || true' EXIT
for _ in $(seq 1 60); do
  if "${RUNTIME}" exec "${PG_NAME}" pg_isready -U postgres -d quotesdb >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

cd "${ROOT}/frontend"
pnpm run test:e2e
