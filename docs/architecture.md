# Architecture

```text
Browser -> Vite (web) --proxy--> Auth.Api (/api/auth/*)
                              -> Quotes.Api (/api/v1/quotes/*)
Quotes.Api validates JWT locally (JwtBearer middleware)
Auth.Api POST /api/auth/validate remains for optional introspection
Aspire AppHost orchestrates processes + YARP gateway (publish) + Docsify
```

## Projects

| Path / resource | Role |
|-----------------|------|
| `src/AppHost` (`auth` orchestration) | Aspire AppHost |
| `src/ServiceDefaults` | Shared Serilog, OTEL, Scalar/OpenAPI, JwtBearer auth + scope policies, ErrorOr→ProblemDetails mapping, correlation |
| `src/Auth` → `auth-api` | Login + JWT issue/validate (DDD layers) |
| `src/Quotes` → `quotes-api` | Random quote; JwtBearer protects `/api/v1/quotes` |
| `web` | React + TypeScript Vite SPA |
| `gateway` | YARP routes `/api/auth` and `/api/v1/quotes`; serves static SPA on publish |
| `docs` | Docsify + combined Scalar reference |

## Correlation

Header `X-Correlation-Id` is created or accepted on each request, returned from login, and reused by the UI on quote calls. Serilog and OTEL scopes/tags carry the same id.

## Authentication

Quotes uses `AddStandardJwtAuthentication` / `UseStandardAuthentication` from ServiceDefaults (JwtBearer + `RequireAuthorization` on the `/api/v1/quotes` group; reads require the `quotes:read` scope policy and writes the `quotes:write` policy, so a valid token alone grants nothing). Auth and Quotes share the same `Jwt` issuer, audience, and signing key — in Development it comes from user-secrets (or the Aspire `jwt-signing-key` parameter), never from committed files, and Production startup rejects the public development key. Auth `POST /api/auth/validate` is an RFC 7662-style introspection endpoint (invalid tokens answer `200 {valid: false}`; only a missing token is a 400); Quotes no longer calls it per request.

## Error flow

Expected failures are `ErrorOr` results from Domain/Application, mapped once at the edge to RFC 9457 ProblemDetails (`ErrorOrHttpExtensions.ToProblem`): `errorCode` + `correlationId` extensions, validation errors under `errors`, `ErrorType` deciding the status code. Exceptions are reserved for infrastructure faults and handled by `UseExceptionHandler`.

## Resilience

Global HttpClient defaults enable Aspire service discovery only. Outbound clients that need Polly should add `Microsoft.Extensions.Http.Resilience` explicitly per client when the first service-to-service call appears — this base does not ship a speculative helper.
