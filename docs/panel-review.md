# Panel Review

**Panel Review** is this repository's multi-agent critical review workflow: one orchestrator (the **Chair**) runs a panel of specialized subagents that read the codebase in parallel and critique it from different expert perspectives, then the Chair synthesizes their reports into a prioritized list of decisions for the owners. It exists because a corporate seed gets cloned — every pattern it ships, good or bad, is amplified across every service that forks it. A single-reviewer pass misses what six focused readers find.

The first run (2026-08-21) audited the then-pending `POST /api/quotes` work, surfaced ten consensus blockers plus ~25 unique findings, and produced the four decisions that drove the seed-hardening change that followed (ErrorOr + ProblemDetails end-to-end, strict layering with the API host as composition root, atomic async persistence ports, and the `quotes:write` scope demo).

## Grounding

The design follows how Anthropic presents multi-agent systems, adapted to a code-review task:

- **Orchestrator–worker pattern.** The Chair plans, delegates, and synthesizes; subagents do focused work in parallel with their own context windows ([How we built our multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system)). This is a *workflow*, not an autonomous agent loop: the orchestration is deterministic, the agents are the steps ([Building Effective Agents](https://www.anthropic.com/engineering/building-effective-agents)).
- **Subagents as repo artifacts.** Each panel member is a Markdown file with YAML frontmatter under `.claude/agents/`, the standard project-level subagent convention (shared through version control).
- **Skills as repeatable entry points.** The orchestration recipe lives in `.claude/skills/panel-review/SKILL.md`, following the [Agent Skills convention](https://agentskills.io/specification) (a folder with a `SKILL.md` whose frontmatter declares `name` and `description`) used by [anthropics/skills](https://github.com/anthropics/skills).

## Architecture

```text
                        ┌──────────────────────┐
                        │       CHAIR          │  (main conversation)
                        │  plans · delegates · │
                        │  verifies · syntheses│
                        └──────────┬───────────┘
                 Stage 1: MAP      │      reads code, produces FACT SHEETS
                                   │      (facts only, no opinions)
        ┌──────────────────────────┼──────────────────────────┐
        ▼                          ▼                          ▼
┌───────────────┐        ┌───────────────┐        ┌───────────────────┐
│ Cartographer 1│        │ Cartographer 2│        │ Cartographer 3    │
│ Domain +      │        │ Api + Infra + │        │ Tests + Docs +    │
│ Application   │        │ ServiceDefaults│       │ pending git diff  │
└───────┬───────┘        └───────┬───────┘        └─────────┬─────────┘
        └──────────────────────────┼──────────────────────────┘
                                   │  fact sheets merged (shared context)
                 Stage 2: JUDGE    │      each critic verifies claims
                                   │      against the real code
        ┌──────────────────────────┼──────────────────────────┐
        ▼                          ▼                          ▼
┌───────────────┐        ┌───────────────┐        ┌───────────────────┐
│ Architecture  │        │ Code          │        │ Platform /        │
│ Critic        │        │ Nitpicker     │        │ Quality Auditor   │
│ (clean arch,  │        │ (merciless    │        │ (tests, security, │
│  DDD, ports)  │        │  line review) │        │  CI, observability)│
└───────┬───────┘        └───────┬───────┘        └─────────┬─────────┘
        └──────────────────────────┼──────────────────────────┘
                                   │
                 Stage 3: DECIDE   │  Chair spot-checks the load-bearing
                                   │  claims itself, then synthesizes:
                                   │  consensus → majority → unique → corrections
                                   ▼
                          Owner decision gate
                          (max 4 decision-shaped questions;
                           findings adopted, forks decided)
                                   ▼
                     Optional Stage 4: IMPLEMENT + VERIFY
                     (repo tooling gates: tests, SonarQube,
                      contract drift, smoke — see Improving)
```

Two properties make it work:

1. **Mapping is separated from judging.** Cartographers produce *facts only*; critics receive the same fact sheets but must verify against the code themselves. In the first run this caught a defect in the fact sheets themselves — a critic disproved one inventory claim (a supposedly mis-cased ProjectReference that did not exist), which is exactly the cross-check the design intends.
2. **Personas are adversarial on purpose.** The three critics are biased in different directions (architecture direction, line-level defect hunting, operational fitness). Findings they *all* reach independently are near-certain signal; findings only one reports are still valuable but get weighted lower.

## Roles

| Role | File | Charter |
|------|------|---------|
| Chair | — (main conversation) | Plans the split, writes self-contained briefs, spot-checks claims, synthesizes, asks the owner the decision questions |
| Cartographer (×N) | `.claude/agents/layer-cartographer.md` | Exhaustive factual inventory of one layer group; no opinions |
| Architecture Critic | `.claude/agents/architecture-critic.md` | Clean Architecture/DDD principal: dependency direction, error modeling, ports, seed fitness |
| Code Nitpicker | `.claude/agents/code-nitpicker.md` | Merciless senior-dev line review: smells, drift, dead code, test smells, hygiene |
| Platform Auditor | `.claude/agents/platform-auditor.md` | Test strategy, security, CI/CD, observability, operational readiness at clone scale |

## The recorded run (2026-08-21)

Scope: the full Quotes bounded context, ServiceDefaults, tests, docs, and the then-uncommitted create-quote change.

| Stage | Agent | Tool calls | Tokens (approx) | Duration |
|-------|-------|-----------|-----------------|----------|
| Map | Cartographer — Domain + Application | 26 | 133k | 2.0 min |
| Map | Cartographer — Api + Infrastructure + ServiceDefaults + hosting | 72 | 607k | 3.2 min |
| Map | Cartographer — Tests + Docs + pending diff | 40 | 358k | 3.7 min |
| Judge | Architecture Critic | 60 | 453k | 6.7 min |
| Judge | Code Nitpicker | 87 | 1,143k | 6.0 min |
| Judge | Platform Auditor | 69 | 846k | 5.1 min |
| — | **Total** | **354** | **~3.5M** | **~27 min wall** (stages run in parallel internally) |

Synthesis outcome:

- **10 consensus blockers** (all three critics, independently): Location header to a nonexistent route; check-then-add race returning 500; fail-open copy-pasted ValidationFilter; four different error body shapes; domain enum leaking through Application; frozen-contract drift (path, 400 shape, no securitySchemes); committed symmetric signing key; Dev-only health endpoints vs. unconditional probes; no CI; sync ports faking async.
- **~25 unique findings** — e.g. the Nitpicker's .sln junk drawer and CRLF/BOM findings; the Architect's `Abstractions`-folder semantics and seed-rows-bypass-invariants; the Auditor's "no test boots the real Program.cs" and constant-time-comparison gaps.
- **1 fact-sheet correction** (the mis-cased ProjectReference claim, disproven by the Nitpicker).
- **0 substantive disagreements** — the critics differed only in severity calibration.

The Chair converted this into four decision questions (error standard, layering strictness, next-round scope, authorization granularity), the owner ratified answers, and the resulting implementation landed with the full suite green in Release and the OpenAPI contracts regenerated through the hermetic pipeline.

## Improving the process

The first run proved the pattern and found real bugs, but it left tooling on the table. Improvements, roughly in order of value:

1. **Make verification a stage, not an afterthought (Stage 4).** The Chair should run the repository's own gates and feed the machine output into the record — critics' claims about "tests don't cover X" or "warnings never run as errors" should be *tool-verified*, not reader-verified:
   - `./scripts/test.sh` — full suite (CI additionally runs it in **Release**, where `TreatWarningsAsErrors` bites; do the same locally: `find tests -name '*.Tests.csproj' | xargs -n1 dotnet test -c Release`).
   - `./scripts/sonar-up.sh` → `./scripts/sonar-scan.sh` → `./scripts/sonar-down.sh` — local SonarQube: coverage on new code, quality gate, duplication. A Panel run that ends red on Sonar is not done; a run that starts with Sonar metrics has objective inputs (hotspots, uncovered paths) for the critics.
   - `./scripts/update-contracts.sh` + `git diff --exit-code docs/openapi/` — hermetic contract regeneration; catches documentation drift mechanically (this check also runs in CI, so the Panel can lean on a PR instead of a local run).
   - `./scripts/test-api.sh` — smoke against a running stack (login, random, create round trip, 409, 400, docs endpoints).
   In the first run, the create-smoke gaps and the Release-only warning failures (including an order-dependent metrics test that only failed in isolation) were discovered *during implementation*; a Stage 4 would have surfaced them during the review.
2. **Two modes: seed audit vs. PR audit.** The recorded run read the whole repo (~3.5M subagent tokens) — appropriate before promoting a seed. For per-PR runs, point the cartographers at `git diff main...HEAD` instead of the tree, keep the same three critics, and cut cost by an order of magnitude.
3. **Objective layering critic.** The Architect's dependency-direction findings would be stronger as a mechanical gate: add a NetArchTest suite asserting the layering table in the README (Domain references nothing; Api never references Domain). Once that test exists, the human-perspective critics stop spending attention on what CI can prove.
4. **Persist run records.** File each run under `docs/panel-review/runs/YYYY-MM-DD.md` (scope, stats, consensus findings, corrections, decisions, follow-ups). The recorded run above is the template; keeping them makes drift between audits visible over time.
5. **Rotate in a fourth critic when the change warrants it** — a security-focused reviewer for auth/token changes, or a performance reviewer for hot paths. Keep the panel small by default: three critics is where consensus voting stays meaningful (majority = 2).

## How to repeat

Everything needed is committed:

```text
docs/panel-review.md               ← this document
.claude/skills/panel-review/       ← SKILL.md: the orchestration recipe (invoke /panel-review)
.claude/agents/                    ← the panel members (Markdown + YAML frontmatter)
  layer-cartographer.md            ← run once per layer group with a Chair-written brief
  architecture-critic.md
  code-nitpicker.md
  platform-auditor.md
```

1. Invoke the skill (`/panel-review`) or ask the agent to run a Panel Review, stating the scope (whole seed, one bounded context, or a diff).
2. The Chair splits the scope into layer groups, briefs one **Cartographer** per group (self-contained prompt: paths, what to report, facts-only), and runs them in parallel.
3. The Chair merges the fact sheets into a shared brief and launches the three **critics** in parallel, each receiving the brief plus its persona charter from `.claude/agents/`.
4. The Chair spot-checks the most load-bearing claims by reading the implicated files itself, then synthesizes: consensus → majority → unique per critic → corrections → the panel verdict.
5. The Chair asks the owner at most four decision-shaped questions and stops there — findings are reported, forks are the owner's to decide.
6. Optionally, after implementation: Stage 4 verification with the repo gates above, and file the run record.

Prompt skeletons, evidence rules (`file:line` for every claim; verify, never trust the fact sheet blindly; severity = Blocker/Major/Minor/Nit), and the output contract for each agent are written into the agent files and the skill, so a future run needs nothing but a scope.

## References

- [How we built our multi-agent research system — Anthropic Engineering](https://www.anthropic.com/engineering/multi-agent-research-system)
- [Building Effective Agents — Anthropic Engineering](https://www.anthropic.com/engineering/building-effective-agents)
- [Equipping agents for the real world with Agent Skills — Anthropic Engineering](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)
- [Agent Skills specification (open standard)](https://agentskills.io/specification)
- [anthropics/skills — official Agent Skills repository](https://github.com/anthropics/skills)
