# AGENTS.md

Working agreements for coding agents in this repository. Humans reviewing the results are
the audience that matters — every rule below exists to keep what lands reviewable.

## Big changes land as stacked pull requests

Never open one large PR. Decompose the change into an ordered chain in which **every
level compiles, passes lint, and passes every CI gate independently**. If an
intermediate level would be red, the split is wrong — redo the split.

The recipe (proven on the PostgreSQL-catalog stack, PRs #13 → #14):

1. **Build and verify the end state first** — all suites green, lint clean. Then snapshot
   uncommitted work to a local backup branch
   (`git checkout -b backup/… && git add -A && git commit`) before splitting, and reset
   the working branch clean. Never push the backup branch.
2. **Choose the split by decision**, bottom to top: schema/foundations first; adapters
   beside the old implementation; no-op plumbing (containers, config, CI steps) as layers
   that a later PR makes load-bearing; then the behavior switch; then pure deletion of the
   old path; docs last. Where a clean seam is impossible, leave a **temporary bridge**
   (e.g. a coexisting DI overload) and remove it in the deletion layer.
3. **Cut branches in order**, each from the previous one's head. Pull file subsets from
   the backup (`git checkout backup/… -- <paths>`); hunk-split files shared by two PRs
   (`AppHost.cs`, `ci.yml`) rather than duplicating them; author intermediate states
   explicitly instead of hoping the final files compile mid-stack.
4. **Verify at the load-bearing levels, not only the tip.** At each level that changes
   behavior, run the suites it could break — at that level — before moving on. Config-only
   layers need a build check. Finish with the full sweep at the tip, then diff the tip
   against the backup: the only acceptable delta is what was deliberately reorganized.
5. **One commit per branch**, message as `type: lowercase imperative`
   (feat / fix / ci / docs / test / refactor / build) — the same convention as the
   repository history.
6. **PR body** = **What** (one paragraph) · **Stack** (part N of M, prev + next links) ·
   **Review pointers** (the three or four things to actually look at) · **Evidence**
   (which suites ran green *at this level*).
7. **Push the branches, open the PRs bottom-up** with each base = the branch below
   (the bottom one → `main`), then register the chain as a GitHub stack:
   `gh extension install github/gh-stack` once, `gh stack link <bottom-pr> … <top-pr>`
   for existing PRs, and `gh stack link <stack-number> <new-pr>` to append later layers.
8. **After registration, merging is bottom-up and automatic** — GitHub rebases and
   retargets the layers above as each PR merges. Do not rebase, force-push, or delete
   mid-stack branches, and do not edit PR bases by hand. Never merge by hand: labeling a
   reviewed PR `merge-me` hands it to the merge-me workflow, which merges green PRs
   itself (stack layers atomically, everything below included) — see
   `.github/workflows/merge-me.yml` and `scripts/merge-me.sh`.

What matters most: every intermediate level green, per-level evidence in the PR bodies,
and a tip that matches the independently verified end state.
