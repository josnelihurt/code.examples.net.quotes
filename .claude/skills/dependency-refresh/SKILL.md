---
name: dependency-refresh
description: Run the dependency-refresh workflow — a mechanical audit of every dependency surface (NuGet central package management, the pnpm frontend, the Aspire SDK pin, Docker/YARP images, GitHub Actions), a batched update plan from a read-only planner agent, a mandatory owner gate that approves every change, and the full repo gate (unit, lint, frontend, BDD, e2e, contract drift) validating each applied batch. Use whenever the user asks to update, upgrade, or refresh dependencies or libraries, to check for outdated or vulnerable packages, or to bring the stack current.
---

# Dependency refresh

Run the repository's reproducible dependency-update workflow. Full background, the verification gate, and the recorded runs live in [docs/dependency-refresh.md](../../../docs/dependency-refresh.md). The planner agent is `.claude/agents/deps-planner.md`; the mechanical audit is `./scripts/audit-deps.sh`.

## Workflow

Follow the stages in order. Nothing is edited before stage 4 answers; everything edited is validated in stage 5 before it is reported as done.

1. **Scope and branch.** `git fetch origin`, then branch `chore/deps-YYYY-MM-DD` from `origin/main` (never refresh on `main`). Confirm `git status --short` is clean. Track the run with the todo list: audit, plan, gate, apply, validate, record.

2. **Audit.** Run `./scripts/audit-deps.sh --out docs/dependency-refresh/runs/YYYY-MM-DD-audit.md` and read the report yourself. A section marked PARTIAL or FAILED is retried once; if it stays failed, its packages are **unknown** for this run and recorded as such — never silently treated as current.

3. **Plan (deps-planner, parallel).** Launch three `deps-planner` agents **in parallel, in a single message** — one per surface (NuGet / frontend / infra). Each brief is self-contained: the absolute repo root, the audit slice verbatim, the manifest paths to read, and the facts-only rule. Collect the three batch tables.

4. **Owner gate.** Merge the tables into one proposal — `Package | Current | Target | Class | Risk | Note`, grouped patch / minor / major / infra pins — and convert the real forks into **at most four decision questions**: which batches are in, the major-version policy (skip, take, or take-separately), whether the slow gates (`./scripts/bdd.sh`, `./scripts/e2e.sh` — both need Podman) run, and whether to ship the PR now or leave the branch. Present both, **ask, and stop.** No manifest is edited before the owner answers.

5. **Apply (approved batches only).** Bump versions in `Directory.Packages.props` (keep the `Aspire.Hosting.*` pins and the AppHost `Sdk="Aspire.AppHost.Sdk/..."` pin on one line — the same-line rule), update `frontend/package.json` via pnpm commands in `frontend/` and commit the regenerated lockfile, and touch image/action pins only if that batch was approved. Never defeat the pnpm security posture (`minimumReleaseAge`, `allowBuilds`, `overrides`) to make an install pass. If a target is refused for being younger than the release gate, report it — do not work around it.

6. **Validate.** Mirror the six CI jobs, fastest first; run the slow gates if the owner enabled them:
   - `./scripts/lint.sh` and `./scripts/test.sh` (dotnet format + unit tests)
   - in `frontend/`: `pnpm lint && pnpm test && pnpm run build`, then `pnpm run gen:api && git diff --exit-code src/api/schema.d.ts`
   - `./scripts/bdd.sh` (Aspire stack; needs Podman) and `./scripts/e2e.sh` (Playwright chromium)
   - contract drift: `./scripts/update-contracts.sh` then `git diff --exit-code docs/openapi/`

   A batch that fails a gate is **reverted** (`git checkout -- <files>`), recorded as blocked with the failing gate and error, and the run continues with the remaining batches. Report results honestly — a skipped or reverted gate is part of the record, not an embarrassment to hide.

7. **Record and ship.** Write `docs/dependency-refresh/runs/YYYY-MM-DD.md`: the audit verdict, the approved plan, what was applied, what was reverted or unknown, and each gate's result. Commit with the repo's commit conventions (see the commit-message skill). Push the branch and open the PR (`chore: refresh dependencies YYYY-MM-DD`) with a `## What changed` table and a `## Validation` section listing every gate and its outcome.

## Hard rules

- **The owner gate is mandatory.** Never jump from plan to manifests, and never ask more than four questions. Batches the owner did not approve do not get applied "while we're here".
- **One PR per run.** A refresh ships exactly its recorded run; mixed-scope commits are not a refresh.
- **Full gate or revert.** An update that has not passed its validation stage is not done — it is either still running or already reverted.
- **Never widen scope.** No drive-by refactors, no manifest cleanup beyond the approved rows. The diff is versions and lockfiles.
- **Respect the security posture.** `minimumReleaseAge`, `allowBuilds` and `overrides` in `frontend/pnpm-workspace.yaml` are policy, not obstacles.
- **Report honestly.** Unknown is unknown; reverted is reverted; passed is passed — the run report says which, per gate.
- **No attribution trailers.** Commits follow the commit-message skill: no `Co-authored-by:`, no AI mentions, never `--no-verify`.
