#!/usr/bin/env bash
# Freeze Auth/Quotes runtime OpenAPI into docs/openapi/ via Dockerfile.build (hermetic).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCKER="${DOCKER:-podman}"
IMAGE_TAG="${CONTRACTS_IMAGE_TAG:-localhost/aspire-quotes-contracts:export}"
OUT_DIR="${ROOT}/docs/openapi"

cd "${ROOT}"
mkdir -p "${OUT_DIR}"

echo "==> Building OpenAPI export image (${DOCKER})"
"${DOCKER}" build -f Dockerfile.build --target contracts -t "${IMAGE_TAG}" .

cid="$("${DOCKER}" create "${IMAGE_TAG}")"
cleanup() {
  "${DOCKER}" rm -f "${cid}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "==> Copying frozen YAML to docs/openapi"
"${DOCKER}" cp "${cid}:/auth.openapi.yaml" "${OUT_DIR}/auth.openapi.yaml"
"${DOCKER}" cp "${cid}:/quotes.openapi.yaml" "${OUT_DIR}/quotes.openapi.yaml"

echo "Updated:"
echo "  ${OUT_DIR}/auth.openapi.yaml"
echo "  ${OUT_DIR}/quotes.openapi.yaml"
echo "Done. Review the diff before committing."
