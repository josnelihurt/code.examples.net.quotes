# Architecture

```text
Browser -> Vite (web) --proxy--> Auth.Api (/api/auth/*)
                              -> Quotes.Api (/api/quotes/*)
Quotes.Api validates JWT locally (JwtBearer middleware)
Auth.Api POST /api/auth/validate remains for introspection demos
Aspire AppHost orchestrates processes + YARP gateway (publish) + Docsify
```

## Projects

| Path / resource | Role |
|-----------------|------|
| `src/AppHost` (`auth` orchestration) | Aspire AppHost |
| `src/ServiceDefaults` | Shared Serilog, OTEL, Scalar/OpenAPI, JwtBearer auth, Polly helpers |
| `src/Auth` → `auth-api` | Login + JWT issue/validate (DDD layers) |
| `src/Quotes` → `quotes-api` | Random quote; JwtBearer protects `/api/quotes` |
| `web` | React + TypeScript Vite SPA |
| `gateway` | YARP routes `/api/auth` and `/api/quotes`; serves static SPA on publish |
| `docs` | Docsify + combined Scalar reference |

## Correlation

Header `X-Correlation-Id` is created or accepted on each request, returned from login, and reused by the UI on quote calls. Serilog and OTEL scopes/tags carry the same id.

## Authentication

Quotes uses `AddStandardJwtAuthentication` / `UseStandardAuthentication` from ServiceDefaults (JwtBearer + `RequireAuthorization` on the `/api/quotes` group). Auth and Quotes share the same `Jwt` issuer, audience, and signing key. Auth `POST /api/auth/validate` is kept as an optional introspection endpoint; Quotes no longer calls it per request.

## Resilience

`AddAuthHttpClientResilience` remains in ServiceDefaults for outbound HttpClients that need explicit Polly (retry + circuit breaker + timeout). Global HttpClient defaults only enable Aspire service discovery (no double retry).
