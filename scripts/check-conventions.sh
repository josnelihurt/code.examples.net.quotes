#!/usr/bin/env bash
# Validates the repository's branch-naming and commit-message rules — the one
# implementation behind the CI conventions job, the opt-in local hooks
# (.githooks/, enabled by scripts/setup-git-hooks.sh) and ad-hoc local use.
# The rules and their rationale live in docs/contributing.md.
#
#   ./scripts/check-conventions.sh --branch <name>     # pushed-branch naming rule
#   ./scripts/check-conventions.sh --range <a>..<b>    # every commit subject in the range
#   ./scripts/check-conventions.sh --title <text>      # a PR title (it becomes the squash commit)
#
# Modes combine (--branch x --range a..b checks both). Append --allow-pr-number
# to tolerate one trailing " (#N)": squash-merge results pushed to main carry it,
# PR titles and in-stack commits must not. Exit 0 = clean, 1 = violations
# (all reported), 2 = usage error.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "${ROOT}"

# Every pushed branch: one of the six prefixes, then a kebab-case name.
BRANCH_REGEX='^(feature|hotfix|chore|docs|ci|fix)/[a-z0-9][a-z0-9-]*[a-z0-9]$'

# Every commit subject and PR title: conventional type, optional scope, optional
# breaking marker, colon, space, summary. Group 4 captures the summary.
TYPES='feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert'
SUBJECT_REGEX="^(${TYPES})(\([a-z0-9/._-]+\))?(!)?: (.+)$"
MAX_SUBJECT=72

ALLOW_PR_NUMBER=0
failures=0

usage() {
  cat >&2 <<'USAGE'
Usage: scripts/check-conventions.sh [--branch NAME] [--range A..B] [--title TEXT]
                                    [--allow-pr-number]
At least one of --branch / --range / --title is required. See docs/contributing.md.
USAGE
}

check_branch() {
  local branch="$1"
  if [[ ! "${branch}" =~ ${BRANCH_REGEX} ]]; then
    echo "branch '${branch}' breaks the naming rule"
    echo "  expected ${BRANCH_REGEX}"
    echo "  (prefix feature/ hotfix/ chore/ docs/ ci/ fix/, then a kebab-case name;"
    echo "   local-only backup/ branches are exempt because they are never pushed)"
    failures=$((failures + 1))
  fi
}

check_subject() {
  local label="$1" subject="$2" why=""
  if [[ "${ALLOW_PR_NUMBER}" == "1" && "${subject}" =~ ^(.*)\ \(#[0-9]+\)$ ]]; then
    subject="${BASH_REMATCH[1]}"
  fi
  if [[ "${ALLOW_PR_NUMBER}" == "0" && "${subject}" =~ \ \(#[0-9]+\)$ ]]; then
    why="${why}  - trailing ' (#N)' only belongs on squash-merged commits on main\n"
  fi
  if [[ "${#subject}" -gt "${MAX_SUBJECT}" ]]; then
    why="${why}  - longer than ${MAX_SUBJECT} characters (${#subject})\n"
  fi
  if [[ ! "${subject}" =~ ${SUBJECT_REGEX} ]]; then
    why="${why}  - must be 'type(scope)!: summary' with type in {${TYPES}}\n"
  else
    local summary="${BASH_REMATCH[4]}"
    if [[ "${summary}" == *. ]]; then
      why="${why}  - summary ends with a period\n"
    fi
    if [[ ! "${summary}" =~ ^[a-z0-9] ]]; then
      why="${why}  - summary must start with a lowercase letter or digit\n"
    fi
  fi
  if [[ "${subject}" == " "* || "${subject}" == *" " ]]; then
    why="${why}  - leading or trailing whitespace\n"
  fi
  if [[ -n "${why}" ]]; then
    echo "${label}: ${subject}"
    printf '%b' "${why}"
    failures=$((failures + 1))
  fi
}

check_range() {
  local range="$1" subject
  while IFS= read -r subject; do
    check_subject "commit in ${range}" "${subject}"
  done < <(git log --no-merges --format='%s' "${range}")
}

checked=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --branch)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      check_branch "$2"
      checked=1
      shift 2
      ;;
    --range)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      check_range "$2"
      checked=1
      shift 2
      ;;
    --title)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      check_subject "title" "$2"
      checked=1
      shift 2
      ;;
    --allow-pr-number)
      ALLOW_PR_NUMBER=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [[ "${checked}" -eq 0 ]]; then
  usage
  exit 2
fi

if [[ "${failures}" -gt 0 ]]; then
  echo "${failures} violation(s) — the rules: docs/contributing.md"
  exit 1
fi
exit 0
