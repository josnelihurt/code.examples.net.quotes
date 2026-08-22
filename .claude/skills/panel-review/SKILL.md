---
name: panel-review
description: Run the multi-agent Panel Review — parallel cartographers map the code, three critics (architecture, nitpicker, platform/quality) judge it, and the chair synthesizes findings into decision questions for the owner. Use whenever the user asks for a panel review, a multi-agent code review, an expert-critique pass, or a seed/PR audit with multiple perspectives.
---

# Panel Review

Run the repository's multi-agent critical review workflow. Full background, the recorded first run, and the folder layout live in [docs/panel-review.md](../../../docs/panel-review.md). The panel members are defined in `.claude/agents/`.

## Workflow

Follow the stages in order. Agents run with the Agent tool; stages 1 and 2 launch their agents **in parallel, in a single message**. All agents are read-only reviewers — they never modify files.

1. **Scope.** Confirm what is under review from the user's request: the whole seed, one bounded context, or a diff (`git diff <base>...HEAD`). Note the mode: full-tree (seed audit) or diff-only (PR audit). Run `git status --short` and `git log --oneline -10` for the current state.

2. **Map (Cartographers, parallel).** Split the scope into 2–4 layer groups (e.g. Domain+Application / Api+Infrastructure+ServiceDefaults / Tests+Docs+diff). Launch one Cartographer (`layer-cartographer`) per group. Each brief must be self-contained: absolute paths, the exhaustive inventory to produce, and the facts-only rule. Collect the fact sheets.

3. **Judge (Criticics, parallel).** Merge the fact sheets into one shared brief (condense; keep every path:line claim). Launch `architecture-critic`, `code-nitpicker`, and `platform-auditor` in parallel, each with: the shared brief, the scope statement, the instruction to verify claims against the code themselves (never trust the brief blindly), and their output contract.

4. **Verify (Chair).** Read the 3–5 most load-bearing files yourself and confirm the headline claims. A critic that disproves a fact-sheet claim is a feature — record it under Corrections, not as noise.

5. **Synthesize (Chair).** Compare the three reports and present:
   - **Consensus** — findings two or all three critics reached independently (strongest signal).
   - **Majority / unique catches** — attributed per critic.
   - **Corrections** — any fact-sheet claims disproven during judging.
   - **Panel verdict** — 3–5 sentences, calibrated to the scope.
6. **Decide (owner gate).** Convert the findings into at most four decision-shaped questions (real forks with recommendations, not "is this OK?"). Ask them and stop: findings are reported; forks belong to the owner. Do not start implementing until the owner answers.

7. **Verify with tooling (when implementation follows).** After any implementation the owner approves, run the repo gates and report results honestly:
   - `find tests -name '*.Tests.csproj' -print0 | xargs -0 -n1 dotnet test -c Release` (Release is where `TreatWarningsAsErrors` applies — the same job CI runs)
   - `./scripts/sonar-up.sh && ./scripts/sonar-scan.sh` (local SonarQube; tear down with `sonar-down.sh`)
   - `./scripts/update-contracts.sh` then `git diff --exit-code docs/openapi/` (hermetic contract drift)
   - `./scripts/test-api.sh` against a running stack (smoke incl. create round trip)

## Evidence rules (all panel members)

- Every finding cites `path:line` and states why it matters at clone scale (this repo is a corporate seed — patterns get copied).
- Severity is Blocker / Major / Minor / Nit; calibrate to the scope, not to enterprise feature completeness.
- Each report ends with: "keep as-is" list (3–5 patterns worth mandating), top 3 priorities, and (critics only) decision questions for the owner.
- Facts-only for cartographers; critics verify before they accuse.

## Hard rules

- **Read-only panel.** No agent creates, edits, deletes, or commits anything. The panel reviews; only the Chair may implement, and only after the owner's answers.
- **Self-contained briefs.** Subagents start fresh: every brief carries the full context it needs, including absolute paths and the repo root.
- **The owner gate is mandatory.** Never skip from synthesis straight to implementation, and never ask more than four questions.
- **Record the run.** Offer to file a run record under `docs/panel-review/runs/YYYY-MM-DD.md` (scope, per-agent stats, consensus findings, corrections, decisions, outcome).
