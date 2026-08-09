#!/usr/bin/env bash
# Shared environment for local Aspire / Podman runs.
export ASPIRE_CONTAINER_RUNTIME="${ASPIRE_CONTAINER_RUNTIME:-podman}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export DOTNET_ENVIRONMENT="${DOTNET_ENVIRONMENT:-Development}"

# Ensure Aspire CLI is on PATH for non-login shells
if [[ -d "${HOME}/.aspire/bin" ]]; then
  export PATH="${HOME}/.aspire/bin:${PATH}"
fi
