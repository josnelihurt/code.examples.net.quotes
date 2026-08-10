#!/usr/bin/env bash
# Runs the unit test suite and writes OpenCover coverage reports for SonarQube.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"

CONFIGURATION="${CONFIGURATION:-Debug}"

cd "${ROOT}"

# Stale reports would otherwise be merged into the next Sonar analysis.
find tests -type d -name TestResults -prune -exec rm -rf {} +

exec dotnet test AspireQuotesPoc.sln \
  --configuration "${CONFIGURATION}" \
  --settings tests/coverlet.runsettings \
  --logger "console;verbosity=normal" \
  "$@"
