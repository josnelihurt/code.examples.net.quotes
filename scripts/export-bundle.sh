#!/usr/bin/env bash
# Create a full git bundle at ~/repo.bundle (replaces any existing file).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BUNDLE="${HOME}/repo.bundle"

if [[ ! -d "${ROOT}/.git" ]]; then
  echo "error: ${ROOT} is not a git repository" >&2
  exit 1
fi

if [[ -e "${BUNDLE}" ]]; then
  rm -f "${BUNDLE}"
  echo "removed existing ${BUNDLE}"
fi

git -C "${ROOT}" bundle create "${BUNDLE}" --all
echo "created ${BUNDLE}"
git -C "${ROOT}" bundle verify "${BUNDLE}"
