# Dependency refresh

**Dependency refresh** is this repository's reproducible library-update workflow: a mechanical audit of every dependency surface, a batched update plan produced by a read-only planner agent, a mandatory **owner gate** where the proposed updates are approved question by question, and the repo's full verification gate run against every applied batch. It exists because dependencies age silently: nothing in the build fails when a package stops being current, so the failure arrives later — as a security advisory you cannot patch quickly because you are three majors behind, or as a wall of accumulated breaking changes. Regular, small, validated refreshes are the cheapest way to hold both risks down.

The workflow was introduced for [net-examples issue #5](https://github.com/josnelihurt/net-examples/issues/5).

## Grounding

The design follows the same Anthropic patterns as the [Panel Review](panel-review.md), adapted to an update task:

- **Mechanical before agentic.** The facts come from `scripts/audit-deps.sh` — a plain script, so every run answers the same questions the same way (`dotnet list package --outdated` / `--vulnerable`, `pnpm outdated` / `pnpm audit`, pin inventory). The agents plan; they do not measure. Reproducibility lives in the script, judgment lives in the planning, and the two never blur.
- **Orchestrator–worker, workflow not loop.** The main conversation orchestrates deterministic stages and launches `deps-planner` subagents in parallel (one per surface) with self-contained briefs — the [multi-agent orchestration](https://www.anthropic.com/engineering/multi-agent-research-system) shape, applied where parallel reading actually pays.
- **Human in the loop at the decision point.** The agent does not pick your updates: it converges the audit into one proposal plus at most four decision questions and stops. Approving the plan is the owner's move — the same approval-gate discipline Claude Code applies before executing a plan.
- **Machine-verified outcomes.** After approval, the repo's own gates (the same seven CI jobs, run locally) decide whether a batch survives. An update that cannot pass the gate is reverted and recorded as blocked, not argued into the branch.

## The update surface

| Surface | Where the version lives | Checked by |
|---------|--------------------------|------------|
| NuGet packages (~25 pins) | `Directory.Packages.props` (central package management) | `dotnet list package --outdated` / `--vulnerable --include-transitive`, per project |
| Aspire AppHost SDK | `src/AppHost/AspireQuotesPoc.AppHost.csproj` (`Sdk="Aspire.AppHost.Sdk/…"`, outside CPM) | pin inventory + same-line check against the `Aspire.Hosting.*` pins |
| Frontend packages | `frontend/package.json` + `frontend/pnpm-lock.yaml` | `pnpm outdated`, `pnpm audit` |
| Docker base image | `Dockerfile.build` (`mcr.microsoft.com/dotnet/sdk:10.0`) | pin inventory |
| Container images (PostgreSQL, pgweb, YARP) | `scripts/images.env` — the repo's one copy of the tags, read by the test fixture, `scripts/e2e.sh` and CI | `scripts/check-image-tags.sh` against the pinned Aspire packages (the CI `image-pins` job) |
| GitHub Actions | `.github/workflows/ci.yml` (`actions/*@v4`, `pnpm/action-setup@v4`) | pin inventory + latest release via `gh api`, best effort |

Three pinned-together rules the planner must carry into every proposal:

- **The Aspire same-line rule.** The AppHost SDK pin and the `Aspire.Hosting.*` versions in CPM — including `Aspire.Hosting.Testing` used by the BDD suite — move together as one line; splitting them is a bug, not a batch.
- **The container image mirror.** `scripts/images.env` is the repo's one copy of the image tags the pinned `Aspire.Hosting.*` packages run (PostgreSQL, pgweb, YARP). An Aspire bump that changes a pinned tag must update that file in the same batch — `scripts/check-image-tags.sh` (the CI `image-pins` job) fails the build otherwise. Bump procedure: raise the package, run the script, let it print the expected tags, bring the file in line.
- **The pnpm security posture.** `frontend/pnpm-workspace.yaml` refuses install scripts except `allowBuilds`, refuses releases younger than 24h (`minimumReleaseAge: 1440`), and pins transitive `postcss` via `overrides` — see [pnpm as the package manager](package-manager-security.md). A refresh reports a target blocked by these rules; it never bypasses them.

## The workflow

The orchestration recipe lives in `.claude/skills/dependency-refresh/SKILL.md` (invoke `/dependency-refresh`); the planner agent is `.claude/agents/deps-planner.md`.

1. **Branch.** `chore/deps-YYYY-MM-DD` from `origin/main` — never refreshed on `main`.
2. **Audit.** `./scripts/audit-deps.sh --out docs/dependency-refresh/runs/YYYY-MM-DD-audit.md`. A section that stays PARTIAL or FAILED marks its packages *unknown* for the run — recorded, not assumed current.
3. **Plan.** Three `deps-planner` agents in parallel (NuGet / frontend / infra), each returning a batch table: `Package | Current | Target | Class | Risk | Note`, with patch / minor / major / infra-pin classes and the constraint flags above.
4. **Owner gate.** One merged proposal plus at most four decision questions: which batches, the major-version policy, whether the slow gates (BDD, e2e — both need Podman) run, ship-now vs leave-branch. The agent asks and stops.
5. **Apply.** Approved batches only: CPM bumps, the AppHost SDK pin (same line), pnpm-driven frontend updates with the regenerated lockfile committed, image/action pins if approved.
6. **Validate.** The verification gate below, fastest first. A failing batch is reverted and recorded as blocked; the run continues.
7. **Record and ship.** `docs/dependency-refresh/runs/YYYY-MM-DD.md` (audit verdict, approved plan, applied / reverted / unknown, per-gate results), one commit, one PR: `chore: refresh dependencies YYYY-MM-DD`.

## The verification gate

Every applied batch must pass what CI would run — the refresh runs the same seven jobs locally instead of discovering drift when the PR opens:

| Check | Script | Proves |
|-------|--------|--------|
| Lint | `./scripts/lint.sh` | `dotnet format` warning-level rules still hold (the CI `lint` job) |
| Unit + API tests | `./scripts/test.sh` — for full fidelity, Release: `find tests -name '*.Tests.csproj' \| xargs -n1 dotnet test -c Release` | behavior and layering tests; Release is where `TreatWarningsAsErrors` bites (the CI `build-and-test` job) |
| Frontend | in `frontend/`: `pnpm lint && pnpm test && pnpm run build`, then `pnpm run gen:api && git diff --exit-code src/api/schema.d.ts` | ESLint, Vitest, `tsc -b`, and that generated API types still match the frozen contracts (the CI `frontend` job) |
| BDD specs | `./scripts/bdd.sh` (needs Podman) | cross-service journeys through the real Aspire stack incl. the YARP gateway (the CI `specs` job) |
| E2E | `./scripts/e2e.sh` | Playwright browser journeys against Release APIs + Vite (the CI `e2e` job) |
| Contract drift | `./scripts/update-contracts.sh` then `git diff --exit-code docs/openapi/` | the frozen OpenAPI documents are unchanged by the update (the CI `contract-drift` job) |
| Image pins | `./scripts/check-image-tags.sh` | `scripts/images.env` still mirrors the tags the pinned Aspire packages run (the CI `image-pins` job) |

The gate is the floor, not the ceiling: a major-version batch that passes everything may still deserve reading the migration notes — the planner's risk column says where to look. See [Testing](testing.md) for what each suite covers and [the documentation gate](documentation-process.md) for the pattern of script-verified claims this page follows.

## How to repeat

Everything needed is committed:

```text
docs/dependency-refresh.md            ← this document
scripts/audit-deps.sh                 ← the mechanical audit (run it standalone any time)
.claude/skills/dependency-refresh/    ← SKILL.md: the orchestration recipe (invoke /dependency-refresh)
.claude/agents/deps-planner.md        ← the read-only planner (one per surface, in parallel)
docs/dependency-refresh/runs/         ← audit + run records, newest last
```

1. Invoke the skill (`/dependency-refresh`) or ask the agent to refresh dependencies.
2. Let the audit run and read the report with the agent; unknown sections are called out, not papered over.
3. Answer the owner gate — at most four questions — and approve exactly the batches you want.
4. Watch the validation gates run; failures revert their batch and land in the run record.
5. Review the PR: a versions-and-lockfiles diff, the run record, and per-gate results.

A standalone `./scripts/audit-deps.sh` (no skill, no changes, exit 0) answers "what is outdated and vulnerable right now?" any time.

## References

- [Building Effective Agents — Anthropic Engineering](https://www.anthropic.com/engineering/building-effective-agents)
- [How we built our multi-agent research system — Anthropic Engineering](https://www.anthropic.com/engineering/multi-agent-research-system)
- [Equipping agents for the real world with Agent Skills — Anthropic Engineering](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)
- [Agent Skills specification (open standard)](https://agentskills.io/specification)
