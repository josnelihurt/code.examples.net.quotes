# Development credentials

The single source of truth for every credential the seed uses **in non-Production environments**. These values are scaffolding so the stack boots offline; every one of them is refused or rejected the moment the environment is Production. Nothing on this page may be reused for anything real.

The CI `secrets-hygiene` job enforces this consolidation: the literals below may appear here, in the one code location that implements them, and in tests that must authenticate — nowhere else.

## Local users (scaffolding identity)

Implemented by [`HardcodedCredentialStore`](../src/Auth/Auth.Infrastructure/HardcodedCredentialStore.cs), which `AddAuthInfrastructure` refuses to register when the environment is Production.

| User | Password | Scopes |
|------|----------|--------|
| `jrb` | `supersecret` | `quotes:read`, `quotes:write` |
| `reader` | `readsecret` | `quotes:read` |

## JWT development signing key

`AspireQuotesPoc-Dev-Signing-Key-32chars!` — the documented Development key. Production startup rejects it (`JwtAuthExtensions` fails fast when the configured key equals this public value, and rejects any key shorter than 32 bytes).

For standalone `dotnet run` in Development (Aspire `run` injects the shared `jwt-signing-key` parameter automatically):

```bash
dotnet user-secrets set "Jwt:SigningKey" "AspireQuotesPoc-Dev-Signing-Key-32chars!" --project src/Auth/Auth.Api
dotnet user-secrets set "Jwt:SigningKey" "AspireQuotesPoc-Dev-Signing-Key-32chars!" --project src/Quotes/Quotes.Api
```

## Ephemeral keys used by automation

| Variable | Used by | Rule |
|----------|---------|------|
| `E2E_SIGNING_KEY` | `frontend/playwright.config.ts` (via `scripts/e2e.sh`) | Any value of at least 32 characters. Required — the config fails fast without it. CI generates a per-run value; locally export anything 32+ chars. |
| `E2E_PG_*` | Standalone e2e catalog — `scripts/e2e.sh`, the CI e2e job and `playwright.config.ts` all read the one file | Throwaway loopback-only values, committed deliberately in [`scripts/e2e.env`](../scripts/e2e.env) because they guard nothing. Never real credentials — those belong to the AppHost or an `AddParameter` secret (see [data storage](data-storage.md)). |
| `Jwt__SigningKey` (in-image) | `Dockerfile.build` OpenAPI export | Generated randomly inside the image at build time; never matches the development key. |
| `Parameters:jwt-signing-key` | BDD stack (`AspireStack`) | Random per test run. |
| `SONAR_ADMIN_PASSWORD` | `scripts/sonar-up.sh` / `sonar-quality-profile.sh` | Required, no committed default. Provide your own when first bringing the local SonarQube container up. |

## Rules

1. New non-Production credentials are documented **here** or they do not exist; the grep gate fails CI on literals outside this file, the implementing code, and tests.
2. Production must never boot on anything from this page — the startup guards are load-bearing, not decorative.
3. Replacing the scaffolding identity is a single-class swap: implement `ICredentialStore` and register it in place of `HardcodedCredentialStore` (see [`Auth.Infrastructure`](../src/Auth/Auth.Infrastructure/README.md)).
