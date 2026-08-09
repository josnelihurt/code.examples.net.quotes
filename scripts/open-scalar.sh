#!/usr/bin/env bash
# Print guidance for opening Scalar (interactive API client).
# Scalar is for manual exploration — use ./scripts/test-api.sh for automated smoke tests.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cat <<'EOF'
Scalar API tooling
------------------
Interactive OpenAPI client (not required for automated tests).

Per-service (when Aspire is running — use each resource URL from the dashboard):
  Auth:   {auth-api-base}/scalar
  Quotes: {quotes-api-base}/scalar
  OpenAPI JSON: {base}/openapi/v1.json

Combined reference (Docsify / docs server):
  http://localhost:3001/scalar/
  or: ./scripts/serve-docs.sh  then open /scalar/

Automated smoke (curl, no browser):
  ./scripts/test-api.sh
EOF

# If docsify is already listening, offer to open the combined page
if curl -sf -o /dev/null "http://127.0.0.1:3001/scalar/" 2>/dev/null; then
  echo
  echo "Docs Scalar is up — opening http://127.0.0.1:3001/scalar/"
  if command -v open >/dev/null 2>&1; then
    open "http://127.0.0.1:3001/scalar/"
  fi
fi
