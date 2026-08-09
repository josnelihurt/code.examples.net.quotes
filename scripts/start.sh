#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"
cd "${ROOT}"
echo "ASPIRE_CONTAINER_RUNTIME=${ASPIRE_CONTAINER_RUNTIME}"
echo "ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"
exec aspire run --non-interactive
