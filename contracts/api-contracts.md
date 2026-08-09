# Frozen API contracts

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
