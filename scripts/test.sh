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

# The same per-project glob CI uses (ci.yml build-and-test): solution-wide `dotnet test`
# would sweep in tests/Bdd (the Aspire spec suite), which needs a container runtime and
# minutes, not an inner loop. Extra args are appended to every project's run.
find tests -name '*.Tests.csproj' -print0 \
  | xargs -0 -n1 dotnet test \
    --configuration "${CONFIGURATION}" \
    --settings tests/coverlet.runsettings \
    --logger "console;verbosity=normal" \
    "$@"
