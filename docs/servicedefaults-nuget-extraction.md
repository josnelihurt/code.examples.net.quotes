# ServiceDefaults as a NuGet building block

Research note (2026-08-22). The question: should `src/ServiceDefaults` stop being a
project reference inside this solution and become a versioned NuGet package that any
service imports as a building block? This page records what the ecosystem does, the
benefits and costs, and the recommended path. **Nothing here is implemented yet** —
every snippet is blueprint, not current code.

Decision made at review time: *document only*. If/when we proceed, the target is
**GitHub Packages** as the feed, and **parameterizing the app-specific coupling first**.

## Where we start from

The kit is already close to package-shaped. The zero-`ProjectReference` rule (see
[ServiceDefaults README](../src/ServiceDefaults/README.md) — "the whole point") means every
dependency is a NuGet package under central package management, which is the main
precondition for `dotnet pack`. What still binds the kit to *this* solution:

| Binding | Where | Why it matters for packaging |
|---------|-------|------------------------------|
| Target framework, doc-file settings | inherited from [`Directory.Build.props`](../Directory.Build.props) | A package must declare its TFM; consumers don't inherit our build props |
| `InterceptorsNamespaces` for the OpenAPI XML-comment source generator | [`Directory.Build.props` line 17](../Directory.Build.props) | Without it, consumers' OpenAPI documents lose XML-comment enrichment; a package can carry it itself via `buildTransitive` props |
| Meter name `AspireQuotesPoc`, counters `auth.*` / `quotes.*` | `Telemetry/AppMetrics.cs` | The kit owns a quotes/auth-specific metrics vocabulary |
| Scope policies `quotes:read` / `quotes:write`, issuer/audience defaults, dev signing key literal | `JwtAuthExtensions.cs` | Auth defaults name this product's scopes |
| Namespaces `AspireQuotesPoc.ServiceDefaults.*` | everywhere | Cosmetic, but a building block for other services should not carry this repo's name |
| `InternalsVisibleTo ServiceDefaults.Tests` | [csproj](../src/ServiceDefaults/AspireQuotesPoc.ServiceDefaults.csproj) | Internals (`ProblemDetailsFactory`, OpenAPI transformers) are unit-tested in this repo |
| `IsAspireSharedProject` | csproj | Aspire tooling uses this to auto-reference the project when new projects enlist in orchestration |

Consumers today: `Quotes.Api`, `Auth.Api`, and three test projects
(`ServiceDefaults.Tests`, `Architecture.Tests`, `Auth.Infrastructure.Tests`) — all plain
`ProjectReference`.

## What the ecosystem says

**Aspire's official position: ServiceDefaults is a per-solution template, not a package.**
The [C# service defaults docs](https://aspire.dev/get-started/csharp-service-defaults/)
frame the project as the solution-local home for `Extensions.cs`; `IsAspireSharedProject`
exists so Aspire tooling can wire new projects to it automatically. Microsoft ships no
official ServiceDefaults NuGet package, and the docs' customization story ("build your own
defaults class library from the underlying packages") stays within one solution.

**Multi-repo teams solve exactly this by publishing their own package.** In
[aspire discussion #1137 (multi-repo support)](https://github.com/microsoft/aspire/discussions/1137),
teams describe republishing their service defaults to a private feed (Azure Artifacts and
alikes) so services in other repositories consume one versioned kit. Community examples
exist on nuget.org (e.g. [Nabs.Aspire.ServiceDefaults](https://www.nuget.org/packages/Nabs.Aspire.ServiceDefaults) —
an explicitly opinionated defaults package). This is a known, repeated pattern, not a
novel move.

**The mechanics are standard NuGet.** `dotnet pack` respects central package management,
so the kit's `PackageReference` versions flow into the `.nuspec` unchanged. Settings that
consumers need at build time travel in the package itself: [props and targets in
`build/`/`buildTransitive/` folders](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets)
are auto-imported by consuming projects — the mechanism SourceLink and analyzer packages
use. That is how the OpenAPI interceptor namespace would reach consumers without them
copying our `Directory.Build.props` line.

**The trade-off space is well trodden.** Project references rebuild instantly and are
always in sync; package references buy versioning, independent release cadence and
cross-solution reuse at the cost of publish/version/restore ceremony — the
["should a solution reference its own packaged projects as packages?"](https://softwareengineering.stackexchange.com/questions/401199/should-a-solution-containing-projects-exposed-as-nuget-reference-them-as-package)
debate lands on: *pack when there are genuinely multiple consumers with different
cadences; don't pack prematurely for a single solution*. Community threads
([shared library vs internal package](https://www.reddit.com/r/csharp/comments/x0lc4i/shared_class_library_vs_internal_nuget_package_vs/),
[Aspire across multiple solutions](https://stackoverflow.com/questions/77757079/is-it-possible-to-use-net-aspire-across-multiple-net-solutions))
converge on the same rule.

## Benefits

1. **Single source of truth across solutions.** Today the README's promise — "reuse
   `ServiceDefaults`" — means copying the project into each new solution and letting the
   copies drift. A package makes the kit genuinely referenceable: one repository of truth,
   `Extensions.cs` never forked.
2. **Explicit versioning.** Upgrades become deliberate, comparable and rollback-able
   (`PackageReference Version="1.4.0"`), and breaking API changes announce themselves as
   major-version bumps instead of rippling silently through every consumer's next build.
3. **Independent release cadence.** The platform kit can evolve (new OpenAPI conventions,
   telemetry changes) without forcing every service to rebuild until each opts in — and
   each service upgrades on its own schedule.
4. **One-place dependency patching.** A CVE in OpenTelemetry/Serilog/JwtBearer is fixed by
   one package release; consumers bump one version line instead of N solutions discovering
   the advisory independently.
5. **Faster scaffolding.** A new service is `dotnet add package` + `AddServiceDefaults()`.
   The "cloneable service base" becomes a *referenceable* service base.
6. **Ownership boundary.** Platform-versus-product decisions get their own review surface:
   PRs against the package, tagged releases, a changelog — instead of edits to a shared
   project that every service implicitly rebuilds.

## Costs and risks

1. **Inner-loop friction.** A project reference is always current; a package is
   pack → push → version bump → restore. This is the single biggest day-to-day cost and
   the reason the migration plan below keeps `ProjectReference` for in-repo consumers.
2. **Versioning discipline.** SemVer must be honored (breaking change ⇒ major bump), and
   multiple consumers eventually produce diamond-dependency conflicts (`NU1107`) that
   someone has to arbitrate. GitHub Packages versions are also **immutable** — a released
   number can never be re-pushed, so 0.x churn burns numbers forever.
3. **Debuggability.** Stepping into the kit requires SourceLink + symbol packages
   (`snupkg`); without them, consumers debug decompiled shadows of code they can read but
   not navigate.
4. **The premature-packaging trap.** For one solution, packaging buys nothing the project
   reference doesn't already give and costs ceremony. The payoff starts at the second
   solution/repository consuming the kit — that's the revisit trigger below.
5. **Aspire tooling assumptions.** `aspire add`/IDE enlistment auto-references the
   solution's ServiceDefaults *project* and injects `AddServiceDefaults()`. With a package,
   that automation stops finding a home unless we keep a shim (Phase 4). Relatedly,
   Aspire's own tooling has open rough edges with central package management
   ([aspire #13128](https://github.com/dotnet/aspire/issues/13128)).
6. **Internals and tests.** `InternalsVisibleTo` is assembly-name-based, so
   `ServiceDefaults.Tests` keeps seeing internals even against a package (no strong naming
   here) — but only test assemblies we control. If the kit ever moves to its own
   repository, those tests move with it.

## Recommended approach

Four phases, in order. Phases are cumulative; each is independently valuable.

### Phase 1 — Parameterize the app-specific coupling (in this repo)

Do this *before* any packaging, so the package's 1.0 API is neutral rather than
retrofitted later:

- **Auth:** `AddStandardJwtAuthentication` gains configuration for what is currently
  hard-coded — scope policy definitions (Quotes passes `quotes:read`/`quotes:write`),
  issuer/audience defaults, and the production guard against the known development
  signing key. Options pattern; current values become this repo's configuration, not the
  kit's constants.
- **Metrics:** meter name configurable; either generalize the counter vocabulary
  (`<context>.<usecase>.count` with an `outcome` tag) or let hosts register their counter
  names against a kit-owned helper. The README's rule — "the kit owns the vocabulary; the
  services own the behaviour" — survives; the vocabulary stops naming quotes.
- **Branding:** namespaces and `PackageId` get a neutral name. GitHub Packages requires
  **lowercase** package IDs.

### Phase 2 — Make the project packable

Blueprint csproj additions (the TFM, currently inherited, becomes explicit):

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <IsPackable>true</IsPackable>
  <PackageId>acme.service-defaults</PackageId>          <!-- placeholder branding -->
  <MinVerTagPrefix>v</MinVerTagPrefix>                  <!-- versions from git tags -->
  <Deterministic>true</Deterministic>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  <RepositoryUrl>https://github.com/josnelihurt/AspireQuotesPoc</RepositoryUrl>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="MinVer" PrivateAssets="all" />
  <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
</ItemGroup>
```

MinVer derives the package version from git tags — no version numbers in files, releases
are `git tag v1.2.3` + push. [SourceLink](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink)
makes the kit debuggable at consumers.

Carry the OpenAPI interceptor setting to consumers with
`buildTransitive/acme.service-defaults.props` inside the package (guarded so it doesn't
duplicate our own `Directory.Build.props` line):

```xml
<Project>
  <PropertyGroup Condition="!$(InterceptorsNamespaces.Contains('Microsoft.AspNetCore.OpenApi.Generated'))">
    <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
  </PropertyGroup>
</Project>
```

This is the piece that makes the package self-sufficient: a consumer with a bare project
gets working XML-comment-enriched OpenAPI documents without copying any of our build
configuration.

### Phase 3 — Publish to GitHub Packages

- **Feed:** `https://nuget.pkg.github.com/<owner>/index.json`. CI pushes with the
  workflow's `GITHUB_TOKEN` (`packages: write`); local restores need a PAT with
  `read:packages` — a one-time developer setup to document in [local-dev.md](local-dev.md).
- **[`nuget.config`](../nuget.config):** add the source and a `packageSourceMapping` entry
  pinning the package ID to it (the current `*` → nuget.org mapping stays for everything
  else).
- **CI:** a `package` job in [ci.yml](../.github/workflows/ci.yml) — on tags `v*`,
  `dotnet pack -c Release` then `dotnet nuget push`. Note the root
  `Directory.Build.props` sets `TreatWarningsAsErrors` in Release; the pack job inherits
  that, which is what we want.
- **Immutability:** a pushed version is final. Pre-1.0, prefer clear `-preview.N`
  suffixes over reburned numbers.

### Phase 4 — Migration: hybrid first

Keep `ProjectReference` for all in-repo consumers (they build from source, tests keep
pinning internals, zero inner-loop cost); external solutions consume the package. If/when
a second repository adopts the kit:

- **Shim option for Aspire tooling:** each consuming solution keeps an (almost empty)
  ServiceDefaults project — `IsAspireSharedProject=true` plus a `PackageReference` to the
  package — so `aspire add` and IDE enlistment keep auto-referencing something and new
  services still get `AddServiceDefaults()` injected.
- **Full dogfooding (optional, later):** this repo's APIs switch to the `PackageReference`
  too, so we experience exactly what external consumers do — at the cost of pack/push/bump
  on every kit change. Only worth it once the kit's API has stabilized.
- **End state if the kit grows:** extract to a dedicated building-blocks repository with
  its own versioning and pipeline; `ServiceDefaults.Tests` moves with it, and the
  architecture rules in [architecture.md](architecture.md) keep enforcing that the kit
  stays a platform kit, wherever it lives.

## Decision and revisit trigger

**2026-08-22 — researched and documented; no code changed.** Target feed if pursued:
GitHub Packages. First step if pursued: parameterize the coupling (Phase 1).

**Revisit when:** a second solution or repository wants the kit, or platform ownership
splits from product ownership. Until then the project reference costs nothing that
packaging would recover.

## Sources

- [Aspire — C# Service Defaults (official docs)](https://aspire.dev/get-started/csharp-service-defaults/)
- [microsoft/aspire discussion #1137 — multi-repo support](https://github.com/microsoft/aspire/discussions/1137)
- [microsoft/aspire #13128 — ServiceDefaults tooling and CPM](https://github.com/dotnet/aspire/issues/13128)
- [Nabs.Aspire.ServiceDefaults — community defaults package](https://www.nuget.org/packages/Nabs.Aspire.ServiceDefaults)
- [NuGet — MSBuild props and targets in packages](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets)
- [.NET Blog — Producing packages with Source Link](https://devblogs.microsoft.com/dotnet/producing-packages-with-source-link/)
- [Software Engineering — solutions referencing their own packages](https://softwareengineering.stackexchange.com/questions/401199/should-a-solution-containing-projects-exposed-as-nuget-reference-them-as-package)
- [Reddit — shared library vs internal NuGet package vs copying](https://www.reddit.com/r/csharp/comments/x0lc4i/shared_class_library_vs_internal_nuget_package_vs/)
- [Stack Overflow — Aspire across multiple solutions](https://stackoverflow.com/questions/77757079/is-it-possible-to-use-net-aspire-across-multiple-net-solutions)
- [GitHub docs — working with the NuGet registry in GitHub Packages](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry)
