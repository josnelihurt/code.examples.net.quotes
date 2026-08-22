#!/usr/bin/env bash
# Creates the 'Aspire Quotes way' C# quality profile (a child of the read-only built-in
# 'Sonar way') and activates rules Sonar way misses, notably S1128 — Sonar's counterpart
# of IDE0005 / ReSharper's "Using directive is unnecessary". Idempotent: safe to re-run.
#
# Run once after ./scripts/sonar-up.sh with the Sonar admin password:
#   SONAR_ADMIN_PASSWORD='...' ./scripts/sonar-quality-profile.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"
# shellcheck source=sonar-env.sh
source "${ROOT}/scripts/sonar-env.sh"

PROFILE_NAME="${SONAR_PROFILE_NAME:-Aspire Quotes way}"
PARENT_PROFILE="${SONAR_PARENT_PROFILE:-Sonar way}"
# rule:severity pairs; S1128 pairs with IDE0005 in .editorconfig.
RULES=("csharpsquid:S1128:MINOR")

if ! curl -fsS -o /dev/null "${SONAR_HOST_URL}/api/system/status"; then
  echo "SonarQube is not reachable at ${SONAR_HOST_URL}. Run ./scripts/sonar-up.sh first." >&2
  exit 1
fi

# /api/authentication/validate answers 200 with {"valid":false} for bad credentials,
# so the body has to be inspected rather than the status code.
CREDENTIALS="${SONAR_ADMIN_USER}:${SONAR_ADMIN_PASSWORD}"
body="$(curl -fsS -u "${CREDENTIALS}" "${SONAR_HOST_URL}/api/authentication/validate")"
if [[ "$body" != *'"valid":true'* ]]; then
  echo "Admin login failed. Pass the real password:" >&2
  echo "  SONAR_ADMIN_PASSWORD='...' $0" >&2
  exit 1
fi

api() {
  local endpoint="$1"
  shift
  local out
  if ! out="$(curl -fsS -u "${CREDENTIALS}" "${SONAR_HOST_URL}/${endpoint}" "$@" 2>&1)"; then
    echo "Request to ${endpoint} failed:" >&2
    echo "${out}" >&2
    return 1
  fi
  printf '%s' "$out"
}

profile_json() {
  api "api/qualityprofiles/search?language=cs" \
    | python3 -c '
import json,sys
for p in json.load(sys.stdin)["profiles"]:
    if p["name"] == sys.argv[1]:
        print(json.dumps(p))
        break
' "$PROFILE_NAME"
}

PROFILE="$(profile_json)"
if [[ -z "$PROFILE" ]]; then
  echo "Creating profile '${PROFILE_NAME}'..."
  api api/qualityprofiles/create \
    --data-urlencode "language=cs" \
    --data-urlencode "name=${PROFILE_NAME}" >/dev/null
  PROFILE="$(profile_json)"
  if [[ -z "$PROFILE" ]]; then
    echo "Profile was created but could not be found afterwards." >&2
    exit 1
  fi
else
  echo "Profile '${PROFILE_NAME}' already exists."
fi

PROFILE_KEY="$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["key"])' "$PROFILE")"

if [[ "$(python3 -c 'import json,sys; print(json.loads(sys.argv[1]).get("parentName",""))' "$PROFILE")" == "${PARENT_PROFILE}" ]]; then
  echo "Parent already '${PARENT_PROFILE}'."
else
  echo "Setting parent to '${PARENT_PROFILE}'..."
  api api/qualityprofiles/change_parent \
    --data-urlencode "language=cs" \
    --data-urlencode "qualityProfile=${PROFILE_NAME}" \
    --data-urlencode "parentQualityProfile=${PARENT_PROFILE}" >/dev/null
fi

for entry in "${RULES[@]}"; do
  # Rule keys contain colons (csharpsquid:S1128), so split on the last one.
  RULE="${entry%:*}"
  SEVERITY="${entry##*:}"

  active="$(api "api/rules/search?qprofile=${PROFILE_KEY}&activation=true&rule_key=${RULE}" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin).get("total", 0))')"
  if [[ "$active" == "1" ]]; then
    echo "Rule ${RULE} already active."
    continue
  fi

  echo "Activating ${RULE} (${SEVERITY})..."
  api api/qualityprofiles/activate_rule \
    --data-urlencode "key=${PROFILE_KEY}" \
    --data-urlencode "rule=${RULE}" \
    --data-urlencode "severity=${SEVERITY}" >/dev/null
done

# Prefer binding the project explicitly; fall back to the server-wide default profile.
if api api/qualityprofiles/add_project \
  --data-urlencode "language=cs" \
  --data-urlencode "qualityProfile=${PROFILE_NAME}" \
  --data-urlencode "project=${SONAR_PROJECT_KEY}" >/dev/null 2>&1; then
  echo "Project '${SONAR_PROJECT_KEY}' linked to '${PROFILE_NAME}'."
else
  echo "Linking the project failed; making '${PROFILE_NAME}' the default C# profile instead..."
  api api/qualityprofiles/set_default \
    --data-urlencode "language=cs" \
    --data-urlencode "qualityProfile=${PROFILE_NAME}" >/dev/null
fi

echo
echo "Done. Next analysis with the new rules: ./scripts/sonar-scan.sh"
