# Observability in Aspire

After `./scripts/start.sh`, open the Aspire dashboard URL printed in the console.

## Traces

1. Sign in and request a quote from the UI (or curl).
2. Open **Traces**.
3. Find ASP.NET request spans for `auth-api` (login) and `quotes-api` (random quote). JwtBearer validates locally on Quotes (no Quotes→Auth hop).

## Structured logs (Serilog)

1. Open **Structured logs**.
2. Filter by `CorrelationId` (same value returned from login / shown on the quote page).
3. Auth login and Quotes random lines should share that id when the UI reuses it.

## Metrics

Custom meter: `AspireQuotesPoc`

| Metric | Tags |
|--------|------|
| `auth.login.count` | `outcome=success\|failure` |
| `auth.validate.count` | `outcome=success\|failure` |
| `quotes.random.count` | `outcome=success\|not_found` |
| `quotes.create.count` | `outcome=success\|invalid\|conflict\|error` |

Generate traffic, then open **Metrics** and select these instruments to explore values in Aspire.

ASP.NET Core, HttpClient, and runtime metrics from OpenTelemetry instrumentation are also exported.
