# Observability in Aspire

After `./scripts/start.sh`, open the Aspire dashboard URL printed in the console.

## Traces

1. Sign in and request a quote from the UI (or curl).
2. Open **Traces**.
3. Find a span chain: `quotes-api` → `auth-api` (validate).

## Structured logs (Serilog)

1. Open **Structured logs**.
2. Filter by `CorrelationId` (same value returned from login / shown on the quote page).
3. Auth login and Quotes random + Auth validate lines should share that id.

## Metrics

Custom meter: `AspireQuotesPoc`

| Metric | Tags |
|--------|------|
| `auth.login.count` | `outcome=success\|failure` |
| `auth.validate.count` | `outcome=success\|failure` |
| `quotes.random.count` | `outcome=success\|failure` |

Generate traffic, then open **Metrics** and select these instruments to explore values in Aspire.

ASP.NET Core, HttpClient, and runtime metrics from OpenTelemetry instrumentation are also exported.
