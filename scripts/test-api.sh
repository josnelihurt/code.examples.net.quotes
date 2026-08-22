#!/usr/bin/env bash
# Curl smoke test: login -> random quote (does not require Scalar).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=env.sh
source "${ROOT}/scripts/env.sh"

AUTH_URL="${AUTH_URL:-}"
QUOTES_URL="${QUOTES_URL:-}"

discover_port() {
  local pattern="$1"
  lsof -nP -iTCP -sTCP:LISTEN 2>/dev/null | awk -v p="$pattern" '
    $1 ~ p && $9 ~ /127\.0\.0\.1:/ {
      split($9, a, ":"); print a[2]; exit
    }'
}

if [[ -z "$AUTH_URL" ]]; then
  port="$(discover_port 'Auth.Api')"
  if [[ -z "$port" ]]; then
    echo "Auth.Api not listening. Start with ./scripts/start.sh or set AUTH_URL." >&2
    exit 1
  fi
  AUTH_URL="http://127.0.0.1:${port}"
fi

if [[ -z "$QUOTES_URL" ]]; then
  port="$(discover_port 'Quotes.Ap')"
  if [[ -z "$port" ]]; then
    echo "Quotes.Api not listening. Start with ./scripts/start.sh or set QUOTES_URL." >&2
    exit 1
  fi
  QUOTES_URL="http://127.0.0.1:${port}"
fi

CORR="testapi-$(date +%s)"
echo "AUTH_URL=${AUTH_URL}"
echo "QUOTES_URL=${QUOTES_URL}"
echo "X-Correlation-Id=${CORR}"

LOGIN="$(curl -fsS -X POST "${AUTH_URL}/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ${CORR}" \
  -d '{"username":"jrb","password":"supersecret"}')"

TOKEN="$(python3 -c "import json,sys; print(json.loads(sys.argv[1])['accessToken'])" "$LOGIN")"
QUOTE="$(curl -fsS "${QUOTES_URL}/api/v1/quotes/random" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Correlation-Id: ${CORR}")"

echo "login=ok"
echo "quote=${QUOTE}"

# Create round trip: 201, then GET the Location header, then a 409 for a near duplicate.
UNIQUE="Smoke test quote $(date +%s)."
HEADERS="$(curl -fsS -o /dev/null -D - -X POST "${QUOTES_URL}/api/v1/quotes" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ${CORR}" \
  -d "{\"text\":\"${UNIQUE}\",\"author\":\"Smoke Test\"}")"
LOCATION="$(printf '%s' "$HEADERS" | awk 'tolower($1)=="location:" {print $2}' | tr -d '\r')"
echo "created=${LOCATION}"

if [[ -n "$LOCATION" ]]; then
  curl -fsS -o /dev/null -w "location_status=%{http_code}\n" "${QUOTES_URL}${LOCATION}" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "X-Correlation-Id: ${CORR}"
fi

expect_status() {
  local actual="$1" expected="$2" label="$3"
  if [[ "${actual}" != "${expected}" ]]; then
    echo "${label}: expected ${expected}, got ${actual}" >&2
    exit 1
  fi
  echo "${label}=${actual} (ok)"
}

# Same text with '!' instead of '.': same fingerprint, so a 409 is expected.
STATUS="$(curl -sS -o /dev/null -w "%{http_code}" -X POST "${QUOTES_URL}/api/v1/quotes" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ${CORR}" \
  -d "{\"text\":\"${UNIQUE%.}!\",\"author\":\"Somebody Else\"}")"
expect_status "${STATUS}" 409 "duplicate_status"

STATUS="$(curl -sS -o /dev/null -w "%{http_code}" -X POST "${QUOTES_URL}/api/v1/quotes" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ${CORR}" \
  -d '{"text":"short","author":"Smoke Test"}')"
expect_status "${STATUS}" 400 "invalid_status"

# List endpoint: the ratified pagination shape, three items off the first page.
PAGE="$(curl -fsS "${QUOTES_URL}/api/v1/quotes?page=1&pageSize=3" \
  -H "Authorization: Bearer ${TOKEN}" \
  -H "X-Correlation-Id: ${CORR}")"
echo "list=${PAGE}"

# The reader account holds only quotes:read, so its tokens cannot write.
READER_LOGIN="$(curl -fsS -X POST "${AUTH_URL}/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ${CORR}" \
  -d '{"username":"reader","password":"readsecret"}')"
READER_TOKEN="$(python3 -c "import json,sys; print(json.loads(sys.argv[1])['accessToken'])" "$READER_LOGIN")"

READER_RANDOM_STATUS="$(curl -sS -o /dev/null -w "%{http_code}" "${QUOTES_URL}/api/v1/quotes/random" \
  -H "Authorization: Bearer ${READER_TOKEN}" \
  -H "X-Correlation-Id: ${CORR}")"
READER_CREATE_STATUS="$(curl -sS -o /dev/null -w "%{http_code}" -X POST "${QUOTES_URL}/api/v1/quotes" \
  -H "Authorization: Bearer ${READER_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: ${CORR}" \
  -d '{"text":"Reader accounts must not create quotes.","author":"Smoke Test"}')"
expect_status "${READER_RANDOM_STATUS}" 200 "reader_random_status"
expect_status "${READER_CREATE_STATUS}" 403 "reader_create_status"

curl -fsS -o /dev/null -w "scalar_auth=%{http_code}\n" "${AUTH_URL}/scalar/"
curl -fsS -o /dev/null -w "openapi_auth=%{http_code}\n" "${AUTH_URL}/openapi/v1.json"
echo "smoke ok"
