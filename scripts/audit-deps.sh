#!/usr/bin/env bash
# Dependency audit gate for the dependency-refresh workflow (see .claude/skills/dependency-refresh).
#
#   1. nuget    outdated and vulnerable packages, consolidated across src/ and tests/
#   2. pnpm     outdated and audit for frontend/
#   3. infra    version pins that live outside the package managers (AppHost SDK,
#               Aspire same-line rule, Docker/YARP images, GitHub Actions)
#
# The report is the product: a failed check is recorded inside its section, never
# hidden, and the script still exits 0 — this is an audit, not a gate. Sections 1
# and 2 need network (nuget.org, npm registry). NuGet answers come as JSON and are
# consolidated by python3; raw text is kept for the frontend and the infra pins.
#
# Usage: ./scripts/audit-deps.sh [--out <file>]
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT}"

OUT=""
if [[ "${1:-}" == "--out" ]]; then
  OUT="${2:?--out needs a file path}"
elif [[ -n "${1:-}" ]]; then
  echo "Usage: $0 [--out <file>]" >&2
  exit 2
fi

REPORT="$(mktemp)"
WORK="$(mktemp -d)"
trap 'rm -rf "${REPORT}" "${WORK}"' EXIT

say()  { printf '%s\n' "$*" >> "${REPORT}"; }
note() { printf '==> %s\n' "$*" >&2; }

say "# Dependency audit — $(date '+%Y-%m-%d %H:%M %Z')"
say
say "Repo: $(git remote get-url origin 2>/dev/null || echo '?') · branch: $(git branch --show-current 2>/dev/null || echo '?') · commit: $(git rev-parse --short HEAD 2>/dev/null || echo '?')"
say

# ---------------------------------------------------------------- environment

say "## Environment"
say
for tool in dotnet node pnpm gh python3; do
  if command -v "${tool}" >/dev/null 2>&1; then
    say "- ${tool}: $("${tool}" --version 2>/dev/null | head -1)"
  else
    say "- ${tool}: MISSING"
  fi
done
say

# ------------------------------------------------------------------ 1. nuget

note "nuget: collecting projects"
csprojs=()
while IFS= read -r p; do csprojs+=("${p}"); done < <(find src tests -name '*.csproj' | sort)

note "nuget: restore (${#csprojs[@]} projects; the slow part of the audit)"
i=0
for proj in "${csprojs[@]}"; do
  i=$((i + 1))
  if ! dotnet restore "${proj}" --verbosity quiet >"${WORK}/restore.log" 2>&1; then
    echo "${proj}" >> "${WORK}/restore-failed.txt"
  fi
done

note "nuget: outdated + vulnerable (${#csprojs[@]} projects x 2)"
i=0
for proj in "${csprojs[@]}"; do
  i=$((i + 1))
  if ! dotnet list "${proj}" package --outdated --format json \
        >"${WORK}/out-${i}.json" 2>"${WORK}/out-${i}.err"; then
    echo "${proj}" >> "${WORK}/outdated-failed.txt"
  fi
  if ! dotnet list "${proj}" package --vulnerable --include-transitive --format json \
        >"${WORK}/vuln-${i}.json" 2>"${WORK}/vuln-${i}.err"; then
    echo "${proj}" >> "${WORK}/vulnerable-failed.txt"
  fi
done

python3 - "${WORK}" > "${WORK}/nuget.md" <<'PY'
import glob, json, os, sys

work = sys.argv[1]

def load(prefix):
    datas = []
    for f in sorted(glob.glob(os.path.join(work, prefix + '-*.json'))):
        try:
            with open(f) as fh:
                datas.append(json.load(fh))
        except Exception:
            pass  # the failing project is recorded by the shell wrapper
    return datas

def rows(datas, fields):
    out = {}
    for data in datas:
        for project in data.get('projects', []):
            path = project.get('path', '?')
            for fw in project.get('frameworks', []):
                for pkg in (fw.get('topLevelPackages', [])
                            + fw.get('transitivePackages', [])):
                    key = tuple(pkg.get(f, '?') for f in fields)
                    out.setdefault(key, set()).add(path)
    return out

print('### Outdated (direct, consolidated)')
print()
outdated = rows(load('out'), ('id', 'resolvedVersion', 'latestVersion'))
if not outdated:
    print('No outdated direct packages.')
else:
    print('| Package | Current | Latest | Projects |')
    print('| --- | --- | --- | --- |')
    for (pid, cur, lat), paths in sorted(outdated.items()):
        print(f'| {pid} | {cur} | {lat} | {len(paths)} |')
print()
print('### Vulnerabilities (direct + transitive)')
print()
vuln = rows(load('vuln'), ('id', 'resolvedVersion', 'severity', 'advisoryUrl'))
if not vuln:
    print('No packages with known advisories.')
else:
    print('| Package | Current | Severity | Advisory | Projects |')
    print('| --- | --- | --- | --- | --- |')
    for (pid, cur, sev, url), paths in sorted(vuln.items()):
        print(f'| {pid} | {cur} | {sev} | {url} | {len(paths)} |')
PY

say "## NuGet (src/ + tests/, central package management)"
say
cat "${WORK}/nuget.md" >> "${REPORT}"
say
for kind in restore outdated vulnerable; do
  if [[ -f "${WORK}/${kind}-failed.txt" ]]; then
    say "**${kind} failed for:**"
    say
    sed 's/^/- /' "${WORK}/${kind}-failed.txt" >> "${REPORT}"
    say
  fi
done

# ---------------------------------------------------------------- 2. frontend

note "frontend: pnpm outdated + audit"
say "## Frontend (pnpm)"
say
if command -v pnpm >/dev/null 2>&1; then
  (cd frontend && pnpm outdated) > "${WORK}/pnpm-outdated.txt" 2>&1 || true
  (cd frontend && pnpm audit)     > "${WORK}/pnpm-audit.txt"     2>&1 || true
  say "### pnpm outdated"
  say
  say '```text'
  cat "${WORK}/pnpm-outdated.txt" >> "${REPORT}"
  say '```'
  say
  say "### pnpm audit"
  say
  say '```text'
  cat "${WORK}/pnpm-audit.txt" >> "${REPORT}"
  say '```'
  say
else
  say "pnpm not on PATH — section FAILED."
  say
fi
say "Security posture in force (frontend/pnpm-workspace.yaml): pnpm refuses install scripts except \`allowBuilds\`, refuses releases younger than \`minimumReleaseAge\` minutes, and pins \`overrides\`:"
say
say '```yaml'
cat frontend/pnpm-workspace.yaml >> "${REPORT}"
say '```'
say

# ------------------------------------------------------------------- 3. infra

note "infra: pins outside the package managers"
say "## Infra pins"
say

sdk_pin="$(grep -hoE 'Aspire\.AppHost\.Sdk/[0-9][0-9A-Za-z.\-]*' src/AppHost/*.csproj | head -1)"
sdk="${sdk_pin##*/}"
say "Aspire AppHost SDK: \`${sdk:-NOT FOUND}\` (src/AppHost/AspireQuotesPoc.AppHost.csproj — outside CPM, bump by hand)"
say

aspire_versions="$(grep -oE 'Include="Aspire[^"]*" Version="[0-9.]+"' Directory.Packages.props \
  | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | sort -u)"
aspire_count="$(wc -l <<<"${aspire_versions}" | tr -d ' ')"
if [[ "${aspire_count}" -eq 1 && "${aspire_versions}" == "${sdk}" ]]; then
  say "Aspire line: consistent — \`${sdk}\` everywhere (SDK pin, \`Aspire.Hosting.*\`, \`Aspire.Hosting.Testing\`)."
else
  say "Aspire line: DIVERGES — SDK \`${sdk:-?}\` vs CPM \`${aspire_versions//$'\n'/, }\`. Keep the AppHost SDK pin and the Aspire.Hosting.* packages (incl. Aspire.Hosting.Testing) on the same line."
fi
say

say "Docker images:"
say
say '```text'
grep -nE '^FROM ' Dockerfile.build >> "${REPORT}" || echo "no FROM lines found" >> "${REPORT}"
say '```'
say
say "YARP gateway image references:"
say
say '```text'
grep -ni 'yarp' src/AppHost/AppHost.cs .github/workflows/ci.yml >> "${REPORT}" || echo "no yarp references found" >> "${REPORT}"
say '```'
say

say "GitHub Actions (pinned in .github/workflows/ci.yml; latest via \`gh api\`, best effort):"
say
say "| Action | Pinned | Latest release |"
say "| --- | --- | --- |"
while IFS= read -r action; do
  [[ -z "${action}" ]] && continue
  repo="${action%@*}"
  ref="${action##*@}"
  latest="?"
  if command -v gh >/dev/null 2>&1; then
    latest="$(gh api "repos/${repo}/releases/latest" --jq .tag_name 2>/dev/null || echo '?')"
  fi
  say "| ${repo} | ${ref} | ${latest} |"
done < <(grep -oE 'uses: [^ ]+@[^ ]+' .github/workflows/ci.yml | sed 's/^uses: //' | sort -u)
say

# -------------------------------------------------------------------- status

count_of() {
  if [[ -f "$1" ]]; then wc -l < "$1" | tr -d ' '; else echo 0; fi
}

say "## Section status"
say
restore_failed="$(count_of "${WORK}/restore-failed.txt")"
outdated_failed="$(count_of "${WORK}/outdated-failed.txt")"
vuln_failed="$(count_of "${WORK}/vulnerable-failed.txt")"
if (( restore_failed + outdated_failed + vuln_failed == 0 )); then
  say "- nuget: OK"
else
  say "- nuget: PARTIAL (restore: ${restore_failed}, outdated: ${outdated_failed}, vulnerable: ${vuln_failed} projects failed)"
fi
say "- frontend: $(command -v pnpm >/dev/null 2>&1 && echo OK || echo FAILED)"
say "- infra: OK"
say
say "Audit only — no dependency was changed by this run. Interpretation and batching belong to the dependency-refresh skill."

if [[ -n "${OUT}" ]]; then
  mkdir -p "$(dirname "${OUT}")"
  cp "${REPORT}" "${OUT}"
  note "report written to ${OUT}"
fi
cat "${REPORT}"
