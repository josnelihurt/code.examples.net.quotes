---
name: documentation-set
description: Run the multi-agent documentation pass — parallel cartographers map the code, parallel writers produce the per-project README set, and the orchestrator writes the general system-design page and verifies every claim mechanically. Use whenever the user asks to write, refresh, or audit the architecture documentation, to document a new component or bounded context, or to bring the component READMEs back in sync with the code.
---

# Documentation set

Produce and maintain this repository's two-tier architecture documentation:

- **The general page**, [`docs/system-design.md`](../../../docs/system-design.md) — the whole system as diagrams: context, components, run-mode and publish-mode topology, layering, request lifecycle, frontend, CI.
- **The component pages**, one `README.md` next to each project's source — its layers, DDD concepts, file inventory, call flows, and the tests that pin its rules.

Background, the pipeline diagram, and the recorded first run live in [docs/documentation-process.md](../../../docs/documentation-process.md). The agents are defined in `.claude/agents/doc-cartographer.md` and `.claude/agents/doc-writer.md`.

## The division of labour

| Tier | Owns | Never contains |
|---|---|---|
| Root [`README.md`](../../../README.md) | Intention, the layering table, the domain glossary, conventions, how to run | Per-project detail |
| [`docs/*.md`](../../../docs/) | Policy — the rules, the contracts, the process pages | Per-project detail |
| [`docs/system-design.md`](../../../docs/system-design.md) | The whole-system diagrams and the component index | Rules already stated in `architecture.md` |
| `src/**/README.md`, `frontend/README.md` | Types, invariants, wiring, call flows, DDD rationale for **one** project | Policy already stated above |

Everything links; nothing is restated. Duplication is the failure mode this split exists to prevent.

## Workflow

Agents run with the Agent tool. Stages 2 and 4 launch their agents **in parallel, in a single message**.

1. **Scope and branch.** Establish what changed and where the work lands.
   - `git fetch origin`, then branch from `origin/main`. Documentation passes run in the dedicated worktree when one exists (`git worktree list`); confirm the tree is clean before starting.
   - Decide the mode: **full pass** (every component) or **targeted** (a new or changed component, plus the general page's affected sections).
   - Note anything untracked on another branch. A document must not link to a file that does not exist on *this* branch — say so in the report instead.

2. **Map (Cartographers, parallel).** Split the tree into 2–4 slices that do not overlap — the natural split is one per bounded context plus one for the platform, orchestrator, frontend, build and CI. Launch one `doc-cartographer` per slice. Each brief is self-contained: absolute repo root, the exact paths to read, the inventory to produce, and the facts-only rule. Collect the fact sheets.

3. **Read the spine yourself.** While the cartographers run, read the files the general page depends on directly: the AppHost, the architecture test suite, one domain in full, the CI workflow, and every existing `docs/*.md`. You cannot verify a writer's output against a summary — only against the code.

4. **Write (Writers, parallel).** Partition the component pages so no two writers share a file, then launch one `doc-writer` per partition. Each brief carries: the worktree root, the exact file list, the section template, that partition's fact sheet, the explicit **link-don't-restate** list of sections already owned elsewhere, and the mermaid style rules.

5. **Write the general page and the wiring yourself.** Do not delegate these — they are the synthesis, and they depend on the cross-cutting reading from stage 3.
   - `docs/system-design.md`.
   - `docs/_sidebar.md` and the `docs/README.md` quick links.
   - Cross-links from the root `README.md` solution-layout table and from `docs/architecture.md` into the new component pages.
   - Docsify renders mermaid through the plugin wired in `docs/index.html`; keep that intact.

6. **Verify.** Run the gate and fix what it finds:
   ```bash
   ./scripts/verify-docs.sh
   ```
   It checks markdown links and heading anchors, that every backticked repo path / route / identifier resolves in the code, and that every mermaid fence renders. Then confirm the served site:
   ```bash
   ./scripts/serve-docs.sh    # open http://localhost:3001/#/system-design
   ```
   Finally `./scripts/lint.sh` and `./scripts/test.sh` — a documentation pass must leave both green and must not touch a source file. Confirm with `git status --short`.

7. **Report.** State every file written and its diagram count, then list separately any **code or doc discrepancy** the pass uncovered — a stale narrative, a contradiction between two documents, a missing proxy rule. Do not silently fix these: they are the pass's most valuable output and the owner decides on each one.

## Hard rules

- **Writers write only their partition.** Disjoint file lists are what makes the parallel stage safe.
- **Nothing is invented.** Every claim traces to a path and an identifier, and the verification gate proves it. A fact a writer cannot verify is dropped, not softened.
- **Correct the fact sheet, never the code.** A documentation pass changes documentation. If it finds a bug, it reports the bug.
- **Link, do not restate.** Before writing a paragraph, check whether the root `README.md` or a `docs/` page already owns it.
- **The general page is not delegated.** It is the one document that requires having read across every slice.
- **Verify before reporting.** Never describe the set as done on the strength of an agent's summary; the gate and your own reading are the evidence.
