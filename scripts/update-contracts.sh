#!/usr/bin/env bash
# Freeze Auth/Quotes runtime OpenAPI into docs/openapi/ via Dockerfile.build (hermetic).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOCKER="${DOCKER:-podman}"
# The tag is namespaced per worktree (8-hex hash of the repo root) so two checkouts
# building their OpenAPI export at once don't race the same image tag. CONTRACTS_IMAGE_TAG
# overrides — plain "export" restores the old machine-global tag.
SUFFIX="$(printf '%s' "${ROOT}" | shasum | cut -c1-8)"
IMAGE_TAG="${CONTRACTS_IMAGE_TAG:-localhost/aspire-quotes-contracts:export-${SUFFIX}}"
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
"${DOCKER}" cp "${cid}:/quotes-v0.openapi.yaml" "${OUT_DIR}/quotes-v0.openapi.yaml"
"${DOCKER}" cp "${cid}:/quotes-v1.openapi.yaml" "${OUT_DIR}/quotes-v1.openapi.yaml"

echo "==> Regenerating the SPA API types (frontend/src/api/schema.d.ts)"
(cd "${ROOT}/frontend" && pnpm run gen:api)

echo "Updated:"
echo "  ${OUT_DIR}/auth.openapi.yaml"
echo "  ${OUT_DIR}/quotes-v0.openapi.yaml"
echo "  ${OUT_DIR}/quotes-v1.openapi.yaml"
echo "  frontend/src/api/schema.d.ts"
echo "Done. Review the diff before committing."
