# API contracts

This folder no longer holds OpenAPI YAML. Use the Docsify docs instead:

| What | Where |
|------|--------|
| Frozen OpenAPI YAML | [`docs/openapi/`](../docs/openapi/) (`auth.openapi.yaml`, `quotes-v0.openapi.yaml`, `quotes-v1.openapi.yaml`, `quotes-v2.openapi.yaml`) |
| How to refresh contracts / API notes | [`docs/api.md`](../docs/api.md) |
| Architecture / auth overview | [`docs/architecture.md`](../docs/architecture.md) |

After changing Api DTOs or endpoints:

```bash
./scripts/update-contracts.sh
```

Details are in [`docs/api.md`](../docs/api.md).
