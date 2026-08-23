#!/usr/bin/env bash
# Runs the Playwright BDD suite for the SPA. Builds both APIs in Release first —
# playwright.config.ts points webServer at their DLLs on fixed loopback ports — then
# hands over to Playwright, which boots the APIs and the Vite dev server itself.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"

dotnet build "${ROOT}/src/Auth/Auth.Api/Auth.Api.csproj" --configuration Release
dotnet build "${ROOT}/src/Quotes/Quotes.Api/Quotes.Api.csproj" --configuration Release

cd "${ROOT}/frontend"
exec npm run test:e2e
