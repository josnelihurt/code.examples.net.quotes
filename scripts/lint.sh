#!/usr/bin/env bash
# Lints C# at the same bar as the Release build (TreatWarningsAsErrors): IDE0005 unused
# usings, naming rules, and other warning-level style/analyzer rules from .editorconfig.
# Suggestion-level formatting (IDE0055) stays out, matching the .editorconfig intent.
#
#   ./scripts/lint.sh         # check only, non-zero exit on violations
#   ./scripts/lint.sh --fix   # rewrite files (e.g. removes unused usings)
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "${ROOT}"

# The solution includes frontend/frontend.esproj (VS JavaScript SDK), which a clean .NET
# SDK checkout cannot build, so format the SDK projects individually. CI runs this same
# script; there is no second, divergent lint invocation.
PROJECTS=()
while IFS= read -r -d '' project; do
  PROJECTS+=("${project}")
done < <(find src tests -name '*.csproj' -print0)

if [[ "${1:-}" == "--fix" ]]; then
  for project in "${PROJECTS[@]}"; do
    dotnet format "${project}" --severity warn
  done
elif [[ -n "${1:-}" ]]; then
  echo "Usage: $0 [--fix]" >&2
  exit 2
else
  for project in "${PROJECTS[@]}"; do
    dotnet format "${project}" --severity warn --verify-no-changes
  done
fi
