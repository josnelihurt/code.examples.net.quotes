#!/usr/bin/env bash
# Runs the Playwright BDD suite for the SPA. Builds both APIs in Release first —
# playwright.config.ts points webServer at their DLLs on loopback ports — starts a
# throwaway PostgreSQL for the quotes catalog (the API migrates + seeds it at boot), then
# hands over to Playwright, which boots the APIs and the Vite dev server itself.
#
# Several worktrees of this repo may run tests at once on this machine (multiple agents
# share one container runtime), so every name and port below is namespaced per worktree:
# an 8-hex hash of the repo root is stable per checkout and unique across checkouts. A
# second worktree therefore gets its own container and ports instead of deleting the
# first one's database or silently reusing its servers. Each derived value stays
# overridable through the E2E_* variables.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"

# Same tag Aspire.Hosting.PostgreSQL pins, so local runs reuse the already-pulled image.
PG_IMAGE="docker.io/library/postgres:18.3"

SUFFIX="$(printf '%s' "${ROOT}" | shasum | cut -c1-8)"
PG_NAME="aspirequotes-e2e-pg-${SUFFIX}"

# Playwright launches the APIs and Vite itself, so their ports must be known before the
# run; derive them from the same hash. The range must sit BELOW the OS ephemeral range
# (Linux hands out 32768+, macOS 49152+) or the kernel can lease these ports to any
# outgoing connection — observed in the wild when the Notes app's IMAP sync broke a run.
# Base 23000–23799 clears that, the repo's own fixed ports (Aspire dashboard/OTLP
# 15142–22035, docs 3001, Sonar 9000, PG 55432), and the usual dev ports; on the unlikely
# clash the run fails loudly at bind time instead of silently mixing runs.
PORT_BASE=$(( 23000 + 0x${SUFFIX:0:4} % 800 ))
AUTH_PORT="${E2E_AUTH_PORT:-$(( PORT_BASE ))}"
QUOTES_PORT="${E2E_QUOTES_PORT:-$(( PORT_BASE + 1 ))}"
VITE_PORT="${E2E_VITE_PORT:-$(( PORT_BASE + 2 ))}"

# The database publishes on an ephemeral loopback port picked by the runtime (empty host
# port in the -p mapping), so two worktrees can never collide on it; the assigned port is
# read back from the container below. Set E2E_PG_PORT to pin it instead.
if [[ -n "${E2E_PG_PORT:-}" ]]; then
  PG_PUBLISH="127.0.0.1:${E2E_PG_PORT}:5432"
else
  PG_PUBLISH="127.0.0.1::5432"
fi

# Honors ASPIRE_CONTAINER_RUNTIME (podman by default here); both CLIs accept these args.
RUNTIME="${ASPIRE_CONTAINER_RUNTIME:-podman}"

dotnet build "${ROOT}/src/Auth/Auth.Api/Auth.Api.csproj" --configuration Release
dotnet build "${ROOT}/src/Quotes/Quotes.Api/Quotes.Api.csproj" --configuration Release

"${RUNTIME}" pull "${PG_IMAGE}"
# Only ever removes this worktree's own leftover from a crashed previous run.
"${RUNTIME}" rm -f "${PG_NAME}" >/dev/null 2>&1 || true
"${RUNTIME}" run -d --name "${PG_NAME}" \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=quotesdb \
  -p "${PG_PUBLISH}" \
  "${PG_IMAGE}"

if [[ -n "${E2E_PG_PORT:-}" ]]; then
  PG_PORT="${E2E_PG_PORT}"
else
  # podman/docker port prints e.g. "127.0.0.1:49153"; keep only what follows the colon.
  PG_PORT="$("${RUNTIME}" port "${PG_NAME}" 5432)"
  PG_PORT="${PG_PORT##*:}"
fi

# Hand the per-worktree values to playwright.config.ts, which reads them with these
# scripts' historical fixed ports as fallbacks.
export E2E_PG_PORT="${PG_PORT}"
export E2E_AUTH_PORT="${AUTH_PORT}"
export E2E_QUOTES_PORT="${QUOTES_PORT}"
export E2E_VITE_PORT="${VITE_PORT}"

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
