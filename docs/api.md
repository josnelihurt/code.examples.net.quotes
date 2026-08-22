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

OpenAPI version and schema names follow what ASP.NET emits (today OpenAPI 3.1, DTO type names). Prefer fixing docs via code/`[Description]` over patching YAML. Security schemes and extra parameters appear only if the running document includes them (configure via OpenAPI transformers in `ServiceDefaults` if needed).

Runtime Scalar on each API still uses live `/openapi/v1.json`; the freeze under `docs/openapi/` is for offline Docsify/Scalar and reviewed PRs.

## Testing: Scalar vs curl

| Tool | Use when |
|------|----------|
| **Scalar** | Interactive try-request / browse OpenAPI (human) |
| **`./scripts/test-api.sh`** | Automated smoke without a browser |
| xUnit (later) | Regression tests |

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

## Endpoints

### Auth

- `POST /api/auth/login` — body `{ username, password }`
- `POST /api/auth/validate` — body `{ accessToken }` or `Authorization: Bearer`

### Quotes

- `GET /api/quotes/random` — requires Bearer JWT (JwtBearer) + optional `X-Correlation-Id`
