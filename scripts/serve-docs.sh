#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"
PORT="${DOCS_PORT:-3001}"
cd "${ROOT}"
echo "Serving Docsify docs on http://localhost:${PORT}"
exec npx --yes docsify-cli serve docs -p "${PORT}" -H 0.0.0.0
