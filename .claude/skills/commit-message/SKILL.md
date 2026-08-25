---
name: commit-message
description: Generate a git commit message from the current changes in this repository, following the repo's existing commit style, and commit only after the user confirms. Use whenever the user asks to commit, to write or draft a commit message, or says things like "commit this" or "create a commit for these changes".
---

# Commit Message

Produce a commit message that matches how this repository already writes commits, then commit only after the user approves.

## Workflow

Follow these steps in order:

1. **Learn the repo's convention.** Run `git log --oneline -15`. Identify the type prefixes actually used (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `refactor:`, …), whether subjects are lowercase, whether commits carry bodies, and typical subject length. The history is the source of truth — if it ever stops using conventional commits, mirror the new style instead of imposing this document's default.

2. **Read the changes.** Before writing anything, get the full picture:
   - `git status --short` — staged, unstaged, and untracked files
   - `git diff --cached` — staged changes
   - `git diff` — unstaged changes
   - Read untracked files when they matter to the summary

   If nothing changed, stop and tell the user.

3. **Write the message.** Summarize what the changes do, not the list of files touched. One subject line in the repo's style; add a short body only if the history shows bodies or the change genuinely needs one. If the staged and unstaged changes are unrelated, say so and propose either one combined message or splitting — let the user decide.

4. **Ask for confirmation.** Present the proposed message in a code block and wait. Never commit before the user explicitly approves it in this conversation.

5. **Commit.** After approval, stage the files that belong to the message (don't sweep in unrelated files) and run `git commit -m "<message>"`. If the user staged files with `git add` already, prefer committing exactly what they staged.

## Format

This repository uses conventional commits with a single lowercase subject line:

```
type: lowercase imperative summary
```

- `type` — one of the enforced conventional types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`, chosen by the dominant nature of the change
- Summary — imperative mood ("add", not "added"), no trailing period, under 72 characters

The full rule set — including the branch-naming rule and the PR-title rule that
CI's `conventions` job enforces — lives in `docs/contributing.md`.

Real examples from this history:

```
docs: add testing and sonarqube guides
fix: raise sonar coverage and simplify endpoint loggers
refactor: extract endpoints and domain interfaces
chore: add sonar tooling and test scripts
```

## Hard rules

- **No attribution trailers.** Never append `Co-authored-by:`, `Generated with …`, or any mention of AI tools or agents. The commit is authored by the configured git user and nothing else.
- **Never bypass hooks** (`--no-verify`). If a hook fails, show the failure and stop.
- **Confirmation is per task.** Approval in an earlier conversation or task does not carry over; ask again.
- **Commit and push are separate actions.** Never push unless the user asks.
