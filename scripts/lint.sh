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

args=(AspireQuotesPoc.sln --severity warn)

if [[ "${1:-}" == "--fix" ]]; then
  exec dotnet format "${args[@]}"
elif [[ -n "${1:-}" ]]; then
  echo "Usage: $0 [--fix]" >&2
  exit 2
else
  exec dotnet format "${args[@]}" --verify-no-changes
fi
