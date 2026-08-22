# API

Transport DTOs live under each service's `*.Api/Contracts` folder. Document them with `[Description]` on types and properties so `Microsoft.AspNetCore.OpenApi` emits schema text into runtime `/openapi/v1.json`.

Do not document Application or Domain types for OpenAPI.

## Frozen OpenAPI (Docsify)

Canonical YAML (do not edit by hand):

- [auth.openapi.yaml](openapi/auth.openapi.yaml)
- [quotes.openapi.yaml](openapi/quotes.openapi.yaml)

### How to refresh

Prerequisite: Podman (default) or Docker. Override with `DOCKER=docker ./scripts/update-contracts.sh`.

After changing endpoints or Api DTOs:

```bash
./scripts/update-contracts.sh
```

This runs a multi-stage [`Dockerfile.build`](../Dockerfile.build) that:

1. Restores and builds `Auth.Api` and `Quotes.Api` inside the .NET SDK image
2. Starts each API on fixed local ports (`--no-launch-profile`)
3. GETs `/openapi/v1.json`
4. Normalizes `servers` to `/` and writes YAML
5. Copies the YAML from a short-lived container into `docs/openapi/`

Review the git diff, then commit the YAML with the code change.

OpenAPI version and schema names follow what ASP.NET emits (today OpenAPI 3.1, DTO type names). Prefer fixing docs via code/`[Description]` over patching YAML. The Bearer security scheme is added by the `BearerSecuritySchemeTransformer` in `ServiceDefaults`, so every authorized operation is documented as secured.

Runtime Scalar on each API still uses live `/openapi/v1.json`; the freeze under `docs/openapi/` is for offline Docsify/Scalar and reviewed PRs.

## Testing: Scalar vs curl

| Tool | Use when |
|------|----------|
| **Scalar** | Interactive try-request / browse OpenAPI (human) |
| **`./scripts/test-api.sh`** | Automated smoke without a browser (login, random, create + Location round trip, 409 duplicate, 400 invalid) |
| xUnit (`./scripts/test.sh`) | Regression tests, incl. full-pipeline `WebApplicationFactory` suites |

Scalar is **not required** to verify the APIs; it is the preferred interactive client from [scalar/scalar](https://github.com/scalar/scalar).

## Runtime Scalar (per service)

When the AppHost is running, open each API resource endpoint:

- OpenAPI JSON: `/openapi/v1.json`
- Scalar UI: `/scalar`
- Aspire dashboard (run mode): click the **Scalar** URL on `auth-api` or `quotes-api`

```bash
./scripts/open-scalar.sh
```

## Combined Scalar (Docsify)

With docs serving (`./scripts/serve-docs.sh` or Aspire `docs` resource):

- [Combined Auth + Quotes reference](scalar/) — loads both OpenAPI YAML files
- Aspire dashboard: **Scalar** link on the `docs` resource (combined Auth+Quotes reference)

## Error contract

Every error response is RFC 9457 ProblemDetails (`application/problem+json`), including the JwtBearer 401 (which also carries `WWW-Authenticate`). `ErrorOr` failures from Domain/Application are mapped once by `ErrorOrHttpExtensions.ToProblem`: error code and correlation id travel as `errorCode` / `correlationId` extensions; validation errors appear under `errors` keyed by error code (domain rules, e.g. `quote.text_too_short`) or property name (transport validation).

## Endpoints

### Auth

- `POST /api/auth/login` — body `{ username, password }`; failure is 401 ProblemDetails (`auth.invalid_credentials`)
- `POST /api/auth/validate` — body `{ accessToken }` or `Authorization: Bearer`; RFC 7662-style introspection: valid and invalid tokens both answer 200 with `{ valid, username }`, only a missing token is 400 ProblemDetails (`auth.token_missing`)

### Quotes

- `GET /api/v1/quotes/random` — requires Bearer JWT **with the `quotes:read` scope** (403 otherwise) + optional `X-Correlation-Id`; 404 ProblemDetails when the catalog is empty
- `GET /api/v1/quotes/{id}` — requires Bearer JWT with `quotes:read`; 404 ProblemDetails for unknown ids
- `POST /api/v1/quotes` — requires Bearer JWT **with the `quotes:write` scope** (403 otherwise); 400 for invalid catalog rules, 409 for near-duplicate fingerprints; 201 returns the `Location` header of the created quote
