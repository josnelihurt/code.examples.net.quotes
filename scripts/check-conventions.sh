#!/usr/bin/env bash
# Thin shim: the canonical conventions script lives in code.examples.ci
# (conventions/scripts/check-conventions.sh, tag v1) and is fetched on demand,
# so the opt-in local hooks (.githooks/commit-msg and pre-push, enabled by
# scripts/setup-git-hooks.sh) keep a single source of truth. The CI job does not
# use this file — it calls the action directly. Requires gh (authenticated) and
# network; exit 2 mirrors the canonical script's usage-error code when the
# fetch itself fails.
set -euo pipefail
canonical="$(mktemp)"
trap 'rm -f "${canonical}"' EXIT
if ! gh api "repos/josnelihurt/code.examples.ci/contents/conventions/scripts/check-conventions.sh?ref=v1" \
     --jq .content | base64 -d > "${canonical}" 2>/dev/null; then
  echo "could not fetch the canonical conventions script from code.examples.ci (tag v1) — is gh authenticated?" >&2
  exit 2
fi
exec bash "${canonical}" "$@"
