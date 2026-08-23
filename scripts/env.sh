#!/usr/bin/env bash
# Shared environment for local Aspire / Podman runs.
export ASPIRE_CONTAINER_RUNTIME="${ASPIRE_CONTAINER_RUNTIME:-podman}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export DOTNET_ENVIRONMENT="${DOTNET_ENVIRONMENT:-Development}"

# Ensure Aspire CLI is on PATH for non-login shells
if [[ -d "${HOME}/.aspire/bin" ]]; then
  export PATH="${HOME}/.aspire/bin:${PATH}"
fi

# Testcontainers (the Postgres-backed unit suites) speaks the Docker API socket, not the
# podman CLI. This block is the only place in the repository that knows about this
# machine's podman layout, and it is shaped so the coupling cannot spread:
#   - it is opt-in by detection: it fires only when DOCKER_HOST is unset AND this exact
#     socket exists, so Docker Desktop, CI runners, and any explicit DOCKER_HOST are
#     untouched;
#   - nothing outside this file (product code, tests, CI) references podman — if the
#     socket path ever moves with a podman upgrade, the block simply no-ops and
#     Testcontainers fails fast with "docker daemon not reachable", pointing back here
#     as the single place to fix;
#   - setting DOCKER_HOST yourself always wins, which is the escape hatch for any other
#     setup.
# Ryuk (Testcontainers' reaper) needs privileges the podman machine does not grant, so
# reaping falls back to Testcontainers' own disposal.
if [[ -z "${DOCKER_HOST:-}" && -S "${HOME}/.local/share/containers/podman/machine/podman.sock" ]]; then
  export DOCKER_HOST="unix://${HOME}/.local/share/containers/podman/machine/podman.sock"
  export TESTCONTAINERS_RYUK_DISABLED=true
fi
