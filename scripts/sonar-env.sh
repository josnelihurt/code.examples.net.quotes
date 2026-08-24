#!/usr/bin/env bash
# Shared settings for the local SonarQube container and scanner.
export SONAR_CONTAINER_NAME="${SONAR_CONTAINER_NAME:-aspirequotes-sonarqube}"
export SONAR_IMAGE="${SONAR_IMAGE:-docker.io/library/sonarqube:26.8.0.126808-community}"
export SONAR_PORT="${SONAR_PORT:-9000}"
export SONAR_HOST_URL="${SONAR_HOST_URL:-http://localhost:${SONAR_PORT}}"
export SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY:-aspire-quotes}"
export SONAR_PROJECT_NAME="${SONAR_PROJECT_NAME:-Aspire Quotes}"

# SonarQube ships with admin/admin and forces a change on first login. The replacement must
# satisfy the server policy: upper, lower, digit and special character. No default is
# committed: export SONAR_ADMIN_PASSWORD before running sonar-up.sh / sonar-quality-profile.sh
# (see docs/dev-credentials.md).
export SONAR_ADMIN_USER="${SONAR_ADMIN_USER:-admin}"
export SONAR_DEFAULT_PASSWORD="${SONAR_DEFAULT_PASSWORD:-admin}"

# Elasticsearch inside SonarQube needs roughly 3 GB; the podman default is smaller.
export SONAR_REQUIRED_VM_MEMORY_MB="${SONAR_REQUIRED_VM_MEMORY_MB:-4096}"
export SONAR_TARGET_VM_MEMORY_MB="${SONAR_TARGET_VM_MEMORY_MB:-6144}"
export PODMAN_MACHINE_NAME="${PODMAN_MACHINE_NAME:-podman-machine-default}"

sonar_state_dir() {
  echo "${1}/.sonar"
}

sonar_token_file() {
  echo "$(sonar_state_dir "$1")/token"
}
