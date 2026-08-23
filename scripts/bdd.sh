#!/usr/bin/env bash
# Runs the Reqnroll spec suite (tests/Bdd) against the real Aspire-orchestrated stack:
# auth-api and quotes-api as separate processes plus the YARP gateway container. Slower
# than scripts/test.sh by design — it proves cross-service journeys, not units.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"

exec dotnet test "${ROOT}/tests/Bdd/AspireQuotesPoc.Specs.csproj" \
  --configuration "${CONFIGURATION:-Debug}" \
  --logger "console;verbosity=normal" \
  "$@"
