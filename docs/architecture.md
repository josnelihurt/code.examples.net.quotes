# Architecture

```text
Browser -> Vite (web) --proxy--> Auth.Api (/api/auth/*)
                              -> Quotes.Api (/api/quotes/*)
Quotes.Api --HttpClient+Polly--> Auth.Api (/api/auth/validate)
Aspire AppHost orchestrates processes + YARP gateway (publish) + Docsify
```

## Projects

| Path / resource | Role |
|-----------------|------|
| `src/AppHost` (`auth` orchestration) | Aspire AppHost |
| `src/ServiceDefaults` | Shared Serilog, OTEL, Scalar/OpenAPI, Polly helpers |
| `src/Auth` → `auth-api` | Login + JWT validate (DDD layers) |
| `src/Quotes` → `quotes-api` | Random quote; validates token via Auth |
| `web` | React + TypeScript Vite SPA |
| `gateway` | YARP routes `/api/auth` and `/api/quotes`; serves static SPA on publish |
| `docs` | Docsify + combined Scalar reference |

## Correlation

Header `X-Correlation-Id` is created or accepted on each request, returned from login, reused by the UI on quote calls, and forwarded Quotes → Auth. Serilog and OTEL scopes/tags carry the same id.

## Resilience

Quotes → Auth uses an explicit Polly pipeline (retry + circuit breaker + timeout) via `AddAuthHttpClientResilience`. Global HttpClient defaults only enable Aspire service discovery (no double retry).
