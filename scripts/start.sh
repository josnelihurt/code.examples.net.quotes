#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"
cd "${ROOT}"
echo "ASPIRE_CONTAINER_RUNTIME=${ASPIRE_CONTAINER_RUNTIME}"
echo "ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}"
# --isolated randomizes the dashboard/service ports and gives this run its own user
# secrets, so several worktrees of this repo can run their AppHost simultaneously
# (https://devblogs.microsoft.com/aspire/aspire-isolated-mode-parallel-development/).
exec aspire run --non-interactive --isolated
