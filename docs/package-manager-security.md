# pnpm as the package manager

Decision note (2026-08-22). The question: the repository ran its entire JavaScript
toolchain on npm — install, scripts, CI, Aspire. After the 2025–2026 wave of npm
supply-chain incidents, is npm still the right default, and if not what replaces it?
This page records the research, the decision, and **the migration that was applied in
the same change** — unlike the [ServiceDefaults note](servicedefaults-nuget-extraction.md),
this one shipped.

**Decision: pnpm, pinned via `packageManager`, with a hardened install policy.**

## Where we start from

Every npm touchpoint lived in one package (`frontend/`) plus the orchestrators around
it — no workspaces, no husky, no `.npmrc`:

| Surface | Was |
|---------|-----|
| Install / scripts | `npm` in `frontend/` (`package-lock.json`, `overrides.postcss`) |
| CI | [ci.yml](../.github/workflows/ci.yml) — `setup-node` with `cache: npm`, `npm ci`, `npx playwright install` |
| AppHost | [`AddViteApp`](../src/AppHost/AppHost.cs) (Aspire defaults to npm), `npx docsify-cli` for the docs site |
| Test infra | Playwright `webServer` spawning `npm run dev`; [scripts](../scripts) calling `npm run` / `npx` |
| Prerequisites | Node 20 in CI — **end-of-life since April 2026** |

## What the ecosystem says

**npm's 2025–2026 incident wave was structural, not bad luck.** The September 2025
"Shai-Hulud" compromises (and the waves that followed) phished maintainer accounts and
pushed malicious releases whose payload ran as **install scripts** — arbitrary code
execution on every developer machine and CI runner that installed the package, purely as
a side effect of `npm install`. npm also **hoists everything into a flat
`node_modules`**, so code can import packages it never declared (phantom dependencies) —
a hiding place for planted code and a recurring source of confusion bugs.

**pnpm removes both default-on mechanisms.** It uses the same registry and the same
packages — the defense is in what the *installer* does:

| npm default | pnpm default |
|-------------|--------------|
| Dependency install scripts (`postinstall`, …) **run automatically** | Scripts **blocked** unless allowlisted (`allowBuilds` in [pnpm-workspace.yaml](../frontend/pnpm-workspace.yaml)) |
| Flat hoisted `node_modules` — anything is importable | Strict symlinked layout — only declared dependencies are reachable |
| New releases installable the second they're published | `minimumReleaseAge: 1440` — a release must be 24 h old before it installs, so fast-moving attacks burn out before they reach us |
| Version chosen by whoever runs the command | `packageManager: pnpm@<exact>` pins one version for every machine and CI |

**Pinning matters as much as the manager.** Corepack — Node's built-in mechanism for
honoring `packageManager` — was voted out of the Node distribution (absent from Node 25),
so the documented path is a standalone pnpm install (`brew install pnpm` or
`npm i -g pnpm`). Once installed, pnpm itself honors the `packageManager` pin and
self-manages the exact version.

**The migration is deliberately boring.** `pnpm import` converted the existing
`package-lock.json` into `pnpm-lock.yaml` preserving every resolved version — the
contract-drift gate (`pnpm run gen:api` + `git diff`) passed unchanged. Aspire supports
pnpm natively: `AddViteApp(...).WithPnpm()` in run mode, and publish mode automatically
uses `pnpm install --frozen-lockfile` when it finds the pnpm lockfile.

## Why it matters for security

1. **No code execution on install unless we say so.** The single highest-impact change:
   a compromised transitive dependency can no longer run its payload during
   `pnpm install`. The only allowlisted build is `esbuild` (platform binary for the Vite
   build) — additions to that list are now a code-review event, visible in
   `pnpm-workspace.yaml`.
2. **No phantom imports.** The strict layout makes "import something you didn't declare"
   fail at build time instead of silently working — planted or hijacked packages have
   nowhere to hide.
3. **A 24-hour quarantine for new releases.** `minimumReleaseAge` means a malicious
   publish is typically caught and unpulled before our lockfile can ever reference it.
4. **One installer version everywhere.** The `packageManager` pin plus CI reading it via
   `pnpm/action-setup` removes "works on my npm" drift between machines.
5. **The runtime exposure shrank too.** Node 20 (EOL) → Node 24 (active LTS) in CI.

**Honest residual risk:** pnpm installs from the same registry with the same package
contents — publisher-side compromise is *mitigated* (delay, blocked scripts), not
eliminated. `pnpm dlx docsify-cli` still fetches on demand like `npx --yes` did. The
next hardening steps if this ever needs to go further: a stricter `minimumReleaseAge`
(one week), `blockExoticSubdeps`, and pnpm's `trustPolicy` — documented in
[pnpm's supply-chain guide](https://pnpm.io/supply-chain-security).

## Standard usage

```bash
brew install pnpm            # or: npm i -g pnpm — once per machine
cd frontend && pnpm install  # install exactly what pnpm-lock.yaml pins
pnpm run dev                 # every npm script works identically: pnpm run <script>
pnpm dlx <tool>              # what npx used to do
```

Command-by-command reference lives in [frontend/README.md](../frontend/README.md);
prerequisites in [local-dev.md](local-dev.md).

## Decision and revisit trigger

**2026-08-22 — researched and migrated in one change.** npm is no longer referenced
anywhere in the repository (the CDN URLs in `docs/index.html` are jsdelivr paths, not
the package manager). Rollback is a single revert — `package-lock.json` returns from
git history.

**Revisit when:** a supply-chain policy stricter than the defaults above becomes
necessary (`trustPolicy`, one-week quarantine), or Aspire's JavaScript integration
changes its package-manager handling.

## Sources

- [pnpm — Supply chain security (official)](https://pnpm.io/supply-chain-security)
- [pnpm — `pnpm import` (lockfile migration)](https://pnpm.io/cli/import)
- [Node.js Security — Hardening npm and pnpm configs post Shai-Hulud](https://www.nodejs-security.com/blog/hardening-your-npm-pnpm-config-for-shai-hulud)
- [Mondoo — npm supply chain security: package manager defenses](https://mondoo.com/blog/npm-supply-chain-security-package-manager-defenses-2026)
- [Aspire — JavaScript apps in the AppHost (package managers, `WithPnpm`)](https://aspire.dev/integrations/frameworks/javascript/)
- [Socket — Node.js TSC votes to stop distributing Corepack](https://socket.dev/blog/node-js-tsc-votes-to-stop-distributing-corepack)
- [pnpm/action-setup — CI setup reading `packageManager`](https://github.com/pnpm/action-setup)
- [Node.js — previous releases (EOL schedule)](https://nodejs.org/en/about/previous-releases)
