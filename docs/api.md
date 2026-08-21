# API

Transport DTOs are documented with `[Description]` on types and properties; runtime OpenAPI picks those up, and the frozen YAML below must stay aligned (see [Contract documentation](../contracts/api-contracts.md#contract-documentation)).

Frozen OpenAPI documents:

- [auth.openapi.yaml](openapi/auth.openapi.yaml)
- [quotes.openapi.yaml](openapi/quotes.openapi.yaml)

Also available under `contracts/` at the repo root.

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

- `GET /api/quotes/random` — requires Bearer token + optional `X-Correlation-Id`
