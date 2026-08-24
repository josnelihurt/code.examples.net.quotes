#!/usr/bin/env bash
# Starts a local SonarQube Community Build in podman and provisions an analysis token.
# Machine-global by design: fixed container name, volumes and port 9000 hold the server
# state — parallel per-worktree instances would each need a full copy. Serialize across
# concurrent agents/worktrees (only one sonar-up/sonar-down at a time).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"
# shellcheck source=sonar-env.sh
source "${ROOT}/scripts/sonar-env.sh"

STATE_DIR="$(sonar_state_dir "${ROOT}")"
TOKEN_FILE="$(sonar_token_file "${ROOT}")"

require_podman_memory() {
  local current
  current="$(podman machine inspect "${PODMAN_MACHINE_NAME}" --format '{{.Resources.Memory}}' 2>/dev/null || echo "")"
  if [[ -z "$current" ]]; then
    echo "Could not inspect podman machine '${PODMAN_MACHINE_NAME}'; skipping the memory check." >&2
    return 0
  fi

  if (( current >= SONAR_REQUIRED_VM_MEMORY_MB )); then
    echo "podman machine memory: ${current} MB (>= ${SONAR_REQUIRED_VM_MEMORY_MB} MB required)"
    return 0
  fi

  cat >&2 <<EOF

SonarQube needs about 3 GB of RAM but '${PODMAN_MACHINE_NAME}' is configured with ${current} MB.
Raising it restarts the machine, which stops every running container.

Commands that will run:
  podman machine stop ${PODMAN_MACHINE_NAME}
  podman machine set --memory ${SONAR_TARGET_VM_MEMORY_MB} ${PODMAN_MACHINE_NAME}
  podman machine start ${PODMAN_MACHINE_NAME}

EOF

  if [[ "${SONAR_ASSUME_YES:-}" != "1" ]]; then
    read -r -p "Resize the podman machine to ${SONAR_TARGET_VM_MEMORY_MB} MB now? [y/N] " reply
    if [[ ! "$reply" =~ ^[Yy]$ ]]; then
      echo "Aborted. Re-run with SONAR_ASSUME_YES=1 to skip this prompt." >&2
      exit 1
    fi
  fi

  podman machine stop "${PODMAN_MACHINE_NAME}"
  podman machine set --memory "${SONAR_TARGET_VM_MEMORY_MB}" "${PODMAN_MACHINE_NAME}"
  podman machine start "${PODMAN_MACHINE_NAME}"
}

start_container() {
  if podman container exists "${SONAR_CONTAINER_NAME}"; then
    if [[ "$(podman inspect -f '{{.State.Running}}' "${SONAR_CONTAINER_NAME}")" == "true" ]]; then
      echo "Container '${SONAR_CONTAINER_NAME}' is already running."
      return 0
    fi
    echo "Starting existing container '${SONAR_CONTAINER_NAME}'..."
    podman start "${SONAR_CONTAINER_NAME}" >/dev/null
    return 0
  fi

  echo "Creating container '${SONAR_CONTAINER_NAME}' from ${SONAR_IMAGE}..."
  podman run --detach \
    --name "${SONAR_CONTAINER_NAME}" \
    --publish "${SONAR_PORT}:9000" \
    --volume "${SONAR_CONTAINER_NAME}-data:/opt/sonarqube/data" \
    --volume "${SONAR_CONTAINER_NAME}-logs:/opt/sonarqube/logs" \
    --volume "${SONAR_CONTAINER_NAME}-extensions:/opt/sonarqube/extensions" \
    "${SONAR_IMAGE}" >/dev/null
}

wait_for_status_up() {
  echo -n "Waiting for SonarQube to report UP"
  for _ in $(seq 1 120); do
    local status
    status="$(curl -fsS "${SONAR_HOST_URL}/api/system/status" 2>/dev/null \
      | python3 -c 'import json,sys; print(json.load(sys.stdin).get("status",""))' 2>/dev/null || true)"
    if [[ "$status" == "UP" ]]; then
      echo " ok"
      return 0
    fi
    echo -n "."
    sleep 5
  done

  echo
  echo "SonarQube did not become ready. Recent container logs:" >&2
  podman logs --tail 40 "${SONAR_CONTAINER_NAME}" >&2
  exit 1
}

# /api/authentication/validate answers 200 with {"valid":false} for bad credentials,
# so the body has to be inspected rather than the status code.
password_works() {
  local body
  body="$(curl -fsS -u "${SONAR_ADMIN_USER}:${1}" \
    "${SONAR_HOST_URL}/api/authentication/validate" 2>/dev/null || true)"
  [[ "$body" == *'"valid":true'* ]]
}

ensure_admin_password() {
  if password_works "${SONAR_ADMIN_PASSWORD}"; then
    echo "Admin password already rotated."
    return 0
  fi

  if ! password_works "${SONAR_DEFAULT_PASSWORD}"; then
    echo "Neither the configured nor the default admin password authenticates." >&2
    echo "Set SONAR_ADMIN_PASSWORD, or reset with ./scripts/sonar-down.sh --purge." >&2
    exit 1
  fi

  echo "Rotating the default admin password..."
  curl -fsS -o /dev/null -u "${SONAR_ADMIN_USER}:${SONAR_DEFAULT_PASSWORD}" \
    "${SONAR_HOST_URL}/api/users/change_password" \
    --data-urlencode "login=${SONAR_ADMIN_USER}" \
    --data-urlencode "previousPassword=${SONAR_DEFAULT_PASSWORD}" \
    --data-urlencode "password=${SONAR_ADMIN_PASSWORD}"
}

ensure_project() {
  local credentials="$1"

  if curl -fsS -u "${credentials}" \
    "${SONAR_HOST_URL}/api/projects/search?projects=${SONAR_PROJECT_KEY}" \
    | grep -q "\"key\":\"${SONAR_PROJECT_KEY}\""; then
    echo "Project '${SONAR_PROJECT_KEY}' already exists."
    return 0
  fi

  echo "Creating project '${SONAR_PROJECT_KEY}'..."
  curl -fsS -o /dev/null -u "${credentials}" \
    "${SONAR_HOST_URL}/api/projects/create" \
    --data-urlencode "project=${SONAR_PROJECT_KEY}" \
    --data-urlencode "name=${SONAR_PROJECT_NAME}"
}

generate_token() {
  local credentials="$1"
  local token_name="${SONAR_PROJECT_KEY}-local"

  # Tokens cannot be read back after creation, so any previous one is revoked and reissued.
  curl -fsS -o /dev/null -u "${credentials}" \
    "${SONAR_HOST_URL}/api/user_tokens/revoke" \
    --data-urlencode "name=${token_name}" || true

  local token
  token="$(curl -fsS -u "${credentials}" \
    "${SONAR_HOST_URL}/api/user_tokens/generate" \
    --data-urlencode "name=${token_name}" \
    --data-urlencode "type=GLOBAL_ANALYSIS_TOKEN" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')"

  if [[ -z "$token" ]]; then
    echo "Failed to generate an analysis token." >&2
    exit 1
  fi

  mkdir -p "${STATE_DIR}"
  printf '%s' "$token" > "${TOKEN_FILE}"
  chmod 600 "${TOKEN_FILE}"
  echo "Analysis token written to ${TOKEN_FILE}"
}

require_podman_memory
start_container
wait_for_status_up
ensure_admin_password

CREDENTIALS="${SONAR_ADMIN_USER}:${SONAR_ADMIN_PASSWORD}"
ensure_project "${CREDENTIALS}"
generate_token "${CREDENTIALS}"

cat <<EOF

SonarQube is ready.
  URL:     ${SONAR_HOST_URL}
  Login:   ${SONAR_ADMIN_USER} / ${SONAR_ADMIN_PASSWORD}
  Project: ${SONAR_PROJECT_KEY}

Next: ./scripts/sonar-scan.sh
EOF
