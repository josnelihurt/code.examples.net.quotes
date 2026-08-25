#!/usr/bin/env bash
# Runs a full SonarQube analysis: scanner begin -> build -> tests with coverage -> scanner end.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"
# shellcheck source=sonar-env.sh
source "${ROOT}/scripts/sonar-env.sh"

TOKEN_FILE="$(sonar_token_file "${ROOT}")"
SONAR_TOKEN="${SONAR_TOKEN:-}"

if [[ -z "$SONAR_TOKEN" ]]; then
  if [[ ! -f "$TOKEN_FILE" ]]; then
    echo "No analysis token at ${TOKEN_FILE}. Run ./scripts/sonar-up.sh first." >&2
    exit 1
  fi
  SONAR_TOKEN="$(cat "${TOKEN_FILE}")"
fi

if ! curl -fsS -o /dev/null "${SONAR_HOST_URL}/api/system/status"; then
  echo "SonarQube is not reachable at ${SONAR_HOST_URL}. Run ./scripts/sonar-up.sh first." >&2
  exit 1
fi

cd "${ROOT}"

dotnet tool restore

# Coverage reports are discovered by glob at scanner end, so stale runs must go first.
find tests -type d -name TestResults -prune -exec rm -rf {} +

# TypeScript coverage and test inclusion moved to net-examples-frontend with the
# SPA's extraction; this scan is the C# side only.
COVERAGE_PATHS="${ROOT}/tests/**/TestResults/**/coverage.opencover.xml"

SONAR_EXCLUSIONS="**/bin/**,**/obj/**,docs/**,src/AppHost/aspire-output/**,**/*.g.cs"
SONAR_TEST_INCLUSIONS="tests/**/*.cs"
SONAR_COVERAGE_EXCLUSIONS="tests/**/*,src/AppHost/**/*,**/Program.cs,**/Contracts/**"

begin_args=(
  "/k:${SONAR_PROJECT_KEY}"
  "/n:${SONAR_PROJECT_NAME}"
  "/d:sonar.host.url=${SONAR_HOST_URL}"
  "/d:sonar.token=${SONAR_TOKEN}"
  "/d:sonar.projectBaseDir=${ROOT}"
  "/d:sonar.cs.opencover.reportsPaths=${COVERAGE_PATHS}"
  "/d:sonar.exclusions=${SONAR_EXCLUSIONS}"
  "/d:sonar.test.inclusions=${SONAR_TEST_INCLUSIONS}"
  "/d:sonar.coverage.exclusions=${SONAR_COVERAGE_EXCLUSIONS}"
  "/d:sonar.scm.disabled=true"
)

dotnet dotnet-sonarscanner begin "${begin_args[@]}"

# The scanner only sees projects compiled between begin and end, so build without incremental reuse.
dotnet build AspireQuotesPoc.sln --configuration Debug --no-incremental

dotnet test AspireQuotesPoc.sln \
  --configuration Debug \
  --no-build \
  --settings tests/coverlet.runsettings

dotnet dotnet-sonarscanner end "/d:sonar.token=${SONAR_TOKEN}"

cat <<EOF

Analysis submitted. Dashboard:
  ${SONAR_HOST_URL}/dashboard?id=${SONAR_PROJECT_KEY}
EOF
