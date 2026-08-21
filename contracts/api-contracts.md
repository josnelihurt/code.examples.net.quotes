# Frozen API contracts

## Contract documentation

Api DTOs under each service's `*.Api/Contracts` folder are the **public HTTP surface**. Document them with `[System.ComponentModel.Description]` on types and properties so `Microsoft.AspNetCore.OpenApi` emits schema descriptions into runtime `/openapi/v1.json` (Scalar).

- Runtime OpenAPI is generated from those attributes.
- `contracts/*.openapi.yaml` (mirrored under `docs/openapi/`) is the reviewed freeze and must stay aligned with the DTO descriptions.
- Do **not** document Application or Domain models for OpenAPI; only transport contracts.

## Aspire resource names

- `auth-api`
- `quotes-api`
- `web`

## Auth

### POST /api/auth/login

Request:

```json
{ "username": "string", "password": "string" }
```

Headers: optional `X-Correlation-Id`

200:

```json
{
  "accessToken": "string",
  "correlationId": "string",
  "expiresIn": 3600,
  "username": "string"
}
```

401:

```json
{ "error": "Invalid credentials" }
```

Hardcoded user: `jrb` / `supersecret`

### POST /api/auth/validate

Request body `{ "accessToken": "string" }` **or** `Authorization: Bearer <token>`

Headers: forward `X-Correlation-Id`

200: `{ "valid": true, "username": "string" }`

401: `{ "valid": false }`

## Quotes

### GET /api/quotes/random

Headers:

- `Authorization: Bearer <token>` (required)
- `X-Correlation-Id` (propagated)

200:

```json
{ "id": "string", "text": "string", "author": "string" }
```

401 if Auth validate fails

## Correlation

Header: `X-Correlation-Id`

Login returns `correlationId`; clients reuse it on subsequent calls. Quotes forwards it to Auth validate.
