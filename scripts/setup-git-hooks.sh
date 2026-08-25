#!/usr/bin/env bash
# Opts this clone into the repository's local git hooks (.githooks): the
# commit-msg and pre-push validators backed by scripts/check-conventions.sh.
# Pure git configuration — no package-manager lifecycle involved, matching the
# frontend's deliberate hookless posture (docs/package-manager-security.md).
#
#   ./scripts/setup-git-hooks.sh                        # enable
#   git config --unset core.hooksPath                    # undo
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "${ROOT}"

git config core.hooksPath .githooks
echo "Local hooks enabled (.githooks: commit-msg, pre-push)."
echo "Undo with: git config --unset core.hooksPath"
