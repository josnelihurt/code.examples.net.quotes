#!/usr/bin/env bash
# Stops and removes the local SonarQube container. Pass --purge to drop its volumes too.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=sonar-env.sh
source "${ROOT}/scripts/sonar-env.sh"

PURGE=0
for arg in "$@"; do
  case "$arg" in
    --purge) PURGE=1 ;;
    *) echo "Unknown argument: $arg (expected --purge)" >&2; exit 1 ;;
  esac
done

if podman container exists "${SONAR_CONTAINER_NAME}"; then
  echo "Removing container '${SONAR_CONTAINER_NAME}'..."
  podman rm --force "${SONAR_CONTAINER_NAME}" >/dev/null
else
  echo "Container '${SONAR_CONTAINER_NAME}' does not exist."
fi

if (( PURGE )); then
  for suffix in data logs extensions; do
    volume="${SONAR_CONTAINER_NAME}-${suffix}"
    if podman volume exists "${volume}"; then
      echo "Removing volume '${volume}'..."
      podman volume rm "${volume}" >/dev/null
    fi
  done
  rm -f "$(sonar_token_file "${ROOT}")"
  echo "Volumes and cached analysis token removed."
fi
