# API

Transport DTOs live under each service's `*.Api/Contracts` folder. Document them with `[Description]` on types and properties so `Microsoft.AspNetCore.OpenApi` emits schema text, and add a class-level `/// <example>{...}</example>` for the sample payload Scalar shows next to the schema.

Do not document Application or Domain types for OpenAPI.

## Documenting operations

Operations (both transports) are documented with XML `///` comments on the handler method / controller action; .NET 10's built-in source generator flows them into the documents:

- `<summary>` → operation summary; `<remarks>` → operation description (use cases, scopes, rate limits, pagination rules).
- `<param name="...">` → parameter description; add `example="..."` for the value Scalar pre-fills. Document **every** parameter (CS1573 otherwise) — infrastructure parameters get a one-line "not part of the HTTP contract".
- `<response code="...">` → response description; carry the public `errorCode` values there.
- The **last** `<param>` tag lands on the request body description, so document the body parameter last (pinned by `OpenApiDocumentationTests`).

Everything textual must be mirrored between `V0/` and `V1/` — `OpenApiParityTests` fails on drift. Error-response `example` bodies come from colocated `[OpenApiProblemExample]` / `.WithProblemExample()` metadata, applied by `OpenApiProblemExampleTransformer` in ServiceDefaults; the document narrative (info description, tag descriptions) from each host's `OpenApiDocs` via `OpenApiDocumentInfo`; both apply identically to every document.

One wiring rule: the generator only intercepts `AddOpenApi` calls whose document name is a **string literal**. Hosts therefore register documents themselves (`builder.Services.AddOpenApi("v0", o => o.ConfigureStandardOpenApi("v0"))`), never through a loop or a constant — `OpenApiDocumentationTests` is the tripwire, because a looped name silently empties every summary while wire tests stay green.

## Frozen OpenAPI (Docsify)

Canonical YAML (do not edit by hand):

- [auth.openapi.yaml](openapi/auth.openapi.yaml)
- [quotes-v0.openapi.yaml](openapi/quotes-v0.openapi.yaml) — controller transport
- [quotes-v1.openapi.yaml](openapi/quotes-v1.openapi.yaml) — minimal-API transport
- [quotes-v2.openapi.yaml](openapi/quotes-v2.openapi.yaml) — proto contract served through the adapter

- [quotes-v3.openapi.json](openapi/quotes-v3.openapi.json) — generated from the v3 proto by
  the freeze pipeline (buf + protoc-gen-openapiv2; Swagger 2.0 — no maintained generator
  emits OpenAPI 3 from `google.api.http` rules), served verbatim at `/openapi/v3.json`.
  Unlike the runtime-exported YAML files above it stays JSON on purpose: the committed
  artifact is the single representation — the bytes the pipeline generates are the bytes
  the drift job diffs and the bytes the API embeds and serves, with no conversion step
  that could drift from what clients actually receive.

See [proto-transports.md](proto-transports.md) for the v2/v3 comparison.

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

## Testing: Scalar vs specs

| Tool | Use when |
|------|----------|
| **Scalar** | Interactive try-request / browse OpenAPI (human) |
| **`./scripts/bdd.sh`** | Automated cross-service journeys without a browser (login, random, create + Location round trip, 409 duplicate, 400 invalid) |
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

- [Combined Auth + Quotes reference](scalar/index.html) — loads both OpenAPI YAML files (the explicit `index.html` matters under `docsify-cli`; the bare `scalar/` path gets its SPA shell)
- Aspire dashboard: **Scalar** link on the `docs` resource (combined Auth+Quotes reference)

## Error contract

Every error response is RFC 9457 ProblemDetails (`application/problem+json`), including the JwtBearer 401 (which also carries `WWW-Authenticate`). `ErrorOr` failures from Domain/Application are mapped once by `ErrorOrHttpExtensions.ToProblem`: error code and correlation id travel as `errorCode` / `correlationId` extensions; validation errors appear under `errors` keyed by error code (domain rules, e.g. `quote.text_too_short`) or property name (transport validation). Transport-validation failures carry `errorCode = validation.request_invalid`, so every 400 a client can meet has the same extensions regardless of which pipeline produced it. Middleware-produced problems (the JwtBearer 401 challenge and the 429 rate-limit rejection) are built by the same envelope (`ProblemDetailsBuilder` in ServiceDefaults), not assembled by hand at each producer.

## Endpoints

### Auth

- `POST /api/v1/auth/login` — body `{ username, password }`; failure is 401 ProblemDetails (`auth.invalid_credentials`). Both auth endpoints are rate-limited per client IP (fixed window); over-limit answers 429 ProblemDetails (`auth.rate_limited`)
- `POST /api/v1/auth/validate` — body `{ accessToken }` or `Authorization: Bearer`; RFC 7662-style introspection: valid and invalid tokens both answer 200 with `{ valid, username }`, only a missing token is 400 ProblemDetails (`auth.token_missing`)

### Quotes

- `GET /api/v1/quotes/random` — requires Bearer JWT **with the `quotes:read` scope** (403 otherwise) + optional `X-Correlation-Id`; 404 ProblemDetails when the catalog is empty
- `GET /api/v1/quotes/{id}` — requires Bearer JWT with `quotes:read`; 404 ProblemDetails for unknown ids
- `GET /api/v1/quotes?page=1&pageSize=20` — requires Bearer JWT with `quotes:read`; the ratified pagination pattern: 1-based page, `pageSize` between 1 and 100 (defaults 1 / 20), 400 ProblemDetails (`quote.invalid_page_request`) outside the range; 200 returns `{ items, page, pageSize, totalItems, totalPages }` in stable catalog order, with an empty `items` array beyond the last page
- `POST /api/v1/quotes` — requires Bearer JWT **with the `quotes:write` scope** (403 otherwise); 400 for invalid catalog rules, 409 for near-duplicate fingerprints; 201 returns the `Location` header of the created quote
- The same four operations exist under `/api/v0/quotes` (MVC), `/api/v2/quotes` (proto contract + adapter; byte-identical wire) and `/api/v3/quotes` (gRPC-JSON transcoding; error bodies are the gRPC status envelope, create answers 200 without `Location`) — see [proto-transports.md](proto-transports.md)
