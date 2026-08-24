#!/usr/bin/env bash
# Fails when the container image pins in scripts/images.env drift from the tags the
# pinned Aspire.Hosting.* packages actually run. The packages are the authority — the
# AppHost resolves its images from them — so every other boot path (test fixtures, e2e,
# CI pre-pulls) must agree with the package, and scripts/images.env is the repo's one
# copy of that agreement. A package bump that misses the pin file, or a hand-edit that
# diverges from the package, turns this script red with the expected/actual pair.
#
#   ./scripts/check-image-tags.sh   # exit 0 = in sync, exit 1 = drift, exit 2 = cannot check
#
# The package tags are read straight from the NuGet global-packages cache: .NET embeds
# string literals as UTF-16, so the DLL bytes are NUL-stripped before grepping. LC_ALL=C
# keeps macOS's BSD tr/grep and CI's GNU ones byte-identical. Extraction is strict —
# anything other than exactly one match per image is a hard failure, so a future package
# reshuffle breaks this gate loudly instead of letting it pass silently.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT}"
export LC_ALL=C

fail=0

# --- repo side: the shared pin file -------------------------------------------------

pin() { # pin KEY -> the value from scripts/images.env (exit 2 if absent)
  local value
  value="$(grep -E "^${1}=" scripts/images.env | head -1 | cut -d= -f2- || true)"
  if [[ -z "${value}" ]]; then
    echo "error: scripts/images.env has no ${1} entry" >&2
    exit 2
  fi
  printf '%s' "${value}"
}

POSTGRES_PIN="$(pin POSTGRES_IMAGE)"
PGWEB_PIN="$(pin PGWEB_IMAGE)"
YARP_PIN="$(pin YARP_IMAGE)"

# --- package side: what the pinned Aspire packages run ------------------------------

pkg_version() { # pkg_version PACKAGE-ID -> CPM version from Directory.Packages.props
  local version
  version="$(grep -F "Include=\"${1}\"" Directory.Packages.props | head -1 | grep -oE 'Version="[^"]+"' | cut -d'"' -f2 || true)"
  if [[ -z "${version}" ]]; then
    echo "error: ${1} is not pinned in Directory.Packages.props" >&2
    exit 2
  fi
  printf '%s' "${version}"
}

package_dll() { # package_dll CACHE-DIR PACKAGE-ID VERSION DLL-NAME -> DLL path (restores once)
  local dll
  dll="$(ls "${1}"/"${2}"/"${3}"/lib/*/"${4}" 2>/dev/null | head -1 || true)"
  if [[ -z "${dll}" ]]; then
    dotnet restore src/AppHost/AspireQuotesPoc.AppHost.csproj >/dev/null
    dll="$(ls "${1}"/"${2}"/"${3}"/lib/*/"${4}" 2>/dev/null | head -1 || true)"
  fi
  if [[ -z "${dll}" ]]; then
    echo "error: ${4} for ${2} ${3} not found in the NuGet cache (even after restore)" >&2
    exit 2
  fi
  printf '%s' "${dll}"
}

# dll_tag DLL PATTERN PREFIX LABEL -> the unique tag in the DLL's literal heap. The
# pattern match includes the image path so it can anchor; PREFIX strips it (and any
# heap separator bytes) before uniquifying, so the same tag reached through different
# separators counts as one.
dll_tag() {
  local tags count
  tags="$(tr -d '\0' < "$1" | grep -oaE "$2" | sed -E "s,^${3},," | sort -u || true)"
  count="$(wc -l <<<"${tags}" | tr -d ' ')"
  if [[ "${count}" -ne 1 ]]; then
    echo "error: cannot read ${4} from ${1##*/} — ${count} distinct matches (expected 1); the package's string layout may have changed" >&2
    exit 2
  fi
  printf '%s' "${tags}"
}

cache_dir="$(dotnet nuget locals global-packages --list | awk '/^global-packages:/ {print $2}')"
if [[ -z "${cache_dir}" ]]; then
  echo "error: cannot locate the NuGet global-packages cache" >&2
  exit 2
fi

pg_version="$(pkg_version Aspire.Hosting.PostgreSQL)"
yarp_version="$(pkg_version Aspire.Hosting.Yarp)"

pg_dll="$(package_dll "${cache_dir%/}" aspire.hosting.postgresql "${pg_version}" Aspire.Hosting.PostgreSQL.dll)"
yarp_dll="$(package_dll "${cache_dir%/}" aspire.hosting.yarp "${yarp_version}" Aspire.Hosting.Yarp.dll)"

# Registry and image path are part of the pattern anchors; only the tag floats. The
# bounded separator window tolerates how the heap concatenates adjacent literals
# (e.g. "library/postgres<TAB>18.3docker.io" and "library/postgres18.3dpage/pgadmin4").
postgres_tag="$(dll_tag "${pg_dll}" 'library/postgres.{0,3}[0-9]+(\.[0-9]+){0,3}' 'library/postgres[^0-9]*' 'the PostgreSQL image tag')"
pgweb_tag="$(dll_tag "${pg_dll}" 'sosedoff/pgweb.{0,3}[0-9]+(\.[0-9]+){0,3}' 'sosedoff/pgweb[^0-9]*' 'the pgweb image tag')"
yarp_tag="$(dll_tag "${yarp_dll}" 'dotnet/nightly/yarp:[0-9A-Za-z][0-9A-Za-z.-]*' 'dotnet/nightly/yarp:' 'the YARP image tag')"

# --- verdict ------------------------------------------------------------------------

compare() { # compare LABEL PINNED EXPECTED
  if [[ "$2" == "$3" ]]; then
    printf '  %-22s ok      %s\n' "$1" "$2"
  else
    printf '  %-22s DRIFT   scripts/images.env pins %s; the package runs %s\n' "$1" "$2" "$3"
    fail=1
  fi
}

echo "Container image pins — scripts/images.env vs Aspire.Hosting.PostgreSQL ${pg_version} / Aspire.Hosting.Yarp ${yarp_version}:"
compare POSTGRES_IMAGE "${POSTGRES_PIN}" "docker.io/library/postgres:${postgres_tag}"
compare PGWEB_IMAGE    "${PGWEB_PIN}"    "docker.io/sosedoff/pgweb:${pgweb_tag}"
compare YARP_IMAGE     "${YARP_PIN}"     "mcr.microsoft.com/dotnet/nightly/yarp:${yarp_tag}"

if [[ "${fail}" -ne 0 ]]; then
  echo
  echo "Drift: update scripts/images.env to the package tags above (bump procedure in"
  echo "docs/dependency-refresh.md), or bump the Aspire.Hosting.* packages so both agree."
  exit 1
fi
