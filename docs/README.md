# Aspire Quotes

Microservice seed on **.NET 10** + **Aspire**: Clean Architecture services (**Auth**, **Quotes**), shared `ServiceDefaults`, React UI, OpenTelemetry, Serilog, OpenAPI/Scalar, and Docsify docs.

See the [repository README](../README.md) for intention, layering rules, and conventions.

## Quick links

- [UI tour](ui-tour.md)
- [System design](system-design.md)
- [Architecture](architecture.md)
- [API reference](api.md)
- [Local development](local-dev.md)
- [Testing](testing.md)
- [SonarQube](sonar.md)
- [Observability in Aspire](observability.md)
- [Documentation process](documentation-process.md)

## Credentials (local scaffolding)

All non-Production credentials — the two local users, the development signing key, the ephemeral keys automation uses — live in [dev-credentials.md](dev-credentials.md), the single source of truth the CI secrets-hygiene gate enforces.
