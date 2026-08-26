# The proto-first transports: v2 and v3

The quote catalog is served four times now. `v0` (MVC) and `v1` (minimal APIs) answer the
question *"does transport style matter?"*. The two newest versions answer a different
pair of questions, both starting from the same artifact: **a single `.proto` file with
`google.api.http` annotations** as the contract-first source of truth.

| | `v2` | `v3` |
|---|---|---|
| Question | Can a proto contract drive the API *without* giving up the established wire contract? | What does the platform's stock runtime give you out of the box? |
| Contract | `V2/Contracts/quotes_v2.proto` (codegen source) | `V3/Contracts/quotes_v3.proto` (drives routing itself) |
| Runtime | Generated `QuoteService.QuoteServiceBase` + a thin HTTP adapter (`V2/Endpoints`) | `Microsoft.AspNetCore.Grpc.JsonTranscoding` — annotations route directly |
| Codegen | Messages **and** service skeleton; no hand-written DTOs | Same, plus the platform serves it |
| Errors | RFC 9457 problem+json via the shared `ProblemDetailsFactory` (byte-identical to v0/v1) | gRPC status envelope `{"code","message","details"}` |
| Create | `201` + `Location` inside v2 | `200`, no `Location` |
| OpenAPI | `/openapi/v2.json` (schemas built from proto descriptors) | `/openapi/v3.json`, **generated from the proto** by the freeze pipeline and served verbatim |
| Parity | Full byte parity, held by `VersionParityTests` (v1↔v2) | Deliberate drift, pinned by `TranscodedQuotes.feature` and the v3 wire tests |

## Why v2 exists (and why it is not "just use transcoding")

The repo's identity is *byte-level parity between transports*: `VersionParityTests`
compares status, media type and body across versions, including the RFC 9457
problem envelope and the `201` + `Location` dance on create. Stock gRPC-JSON transcoding
cannot honor either:

- **Error bodies.** Transcoding renders failures as the
  [gRPC status envelope](https://grpc.io/docs/guides/error/) — `{"code","message","details"}`
  — and there is no supported hook to emit `application/problem+json` instead. Even the
  machine-readable `errorCode` cannot travel: the canonical carrier would be an
  `ErrorInfo` rich-error detail in the `grpc-status-details-bin` trailer, which this
  grpc-dotnet line does not parse when writing transcoded error bodies (see
  [dotnet/aspnetcore#49196](https://github.com/dotnet/aspnetcore/issues/49196) and
  [discussion #59467](https://github.com/dotnet/aspnetcore/discussions/59467)); a packed
  detail makes the response writer throw. v3 therefore documents the message string as
  the only error signal — a real finding, not a bug here.
- **Success semantics.** A unary rpc answers `200`. There is no way to express
  `201 Created` or a `Location` header.
- **Documentation.** Transcoded routes are invisible to ApiExplorer, and the companion
  OpenAPI package is
  [deprecated with no replacement](https://learn.microsoft.com/aspnet/core/grpc/json-transcoding-openapi),
  so the runtime cannot produce `/openapi/v3.json`. v3 therefore *generates* its document
  from the proto instead: `protoc-gen-openapiv2` (driven by buf, since raw protoc refuses
  the plugin on proto3-`optional` fields) turns the contract's comments, `google.api.http`
  rules and `openapiv2_swagger` options into a Swagger 2.0 document, the freeze pipeline
  commits it (`docs/openapi/quotes-v3.openapi.json`), the drift job diffs it, and the API
  serves those exact bytes — the committed file is the single representation: what the
  generator emits, what the drift job diffs, and what `/openapi/v3.json` serves are the
  same bytes, which is also why it stays JSON rather than being converted to the YAML the
  runtime-exported documents use (a conversion would be a second, drifted-able
  representation of what the API actually serves). Swagger 2.0, because no maintained
  generator emits OpenAPI 3 from `google.api.http` rules.

So v2 keeps the platform's codegen (Grpc.Tools messages, service skeleton, descriptors)
and closes the gap with a small, explicit adapter: JSON-PB binding and formatting
(`V2/Proto/ProtoJson.cs`), an `RpcException` ⇄ ErrorOr round trip through trailer metadata
(`GrpcErrorBridge.cs`) so errors flow through the same shared `ProblemDetailsFactory` as
v0/v1, contract validation that mirrors the Data Annotations 400 shape, and an OpenAPI
schema transformer that builds schemas from the proto descriptors
(`V2/OpenApi/ProtoSchemaTransformer.cs`). Everything lives in `V2/`; no Application,
Domain or Infrastructure code changed.

A conformance test (`V2/ProtoContractTests.cs`) reads the descriptors and asserts the
served routes match the `google.api.http` annotations, so the proto cannot silently
stop being the contract.

## Why v3 exists

v3 is the control group: the same contract shape served by
[stock gRPC-JSON transcoding](https://learn.microsoft.com/aspnet/core/grpc/json-transcoding),
wired with `AddGrpc().AddJsonTranscoding()` and `MapGrpcService<>`, with the annotations
themselves doing the routing. Its drift is pinned, not suffered:

- success payloads are the same camelCase JSON (proto's JSON-PB mapping matches what
  v0/v1 emit; the `optional` paging scalars keep `page: 1` present instead of dropped as
  a proto default),
- HTTP status classes survive via gRPC status codes (`NotFound → 404`, `InvalidArgument
  → 400`, `AlreadyExists → 409`),
- 401/403 come from the same JWT middleware in front of everything,
- and everything that differs is asserted: the error envelope, `200`-without-`Location`
  on create, contract violations flowing to *domain* validation (a proto message has no
  Data Annotations layer, so empty fields surface as `quote.text_too_short`), and the
  proto-generated OpenAPI document (served verbatim from the frozen artifact, never
  rendered at runtime).

## Where the versions live now

Each version owns a folder under `src/Quotes/Quotes.Api/` with an `IApiModule` — its
document, its narrative, its services, its endpoints — and `ApiModuleRegistry` lists the
modules in one explicit, greppable place, so `Program.cs` stays agnostic of which
transports exist. Every
contract is self-contained: no version's document or proto mentions another, which is why
adding a transport no longer rewrites the frozen contracts beside it.

## Choosing between them

Use `v2` when the audience is HTTP/JSON clients that must not observe a difference from
v0/v1 — it is the proof that protobuf can be the contract *without* breaking the wire.
Use `v3` when the audience already speaks gRPC-JSON or the frontend tolerates the
platform's conventions — it is less code, and its limits are now enumerated here rather
than discovered in production. New plain-HTTP integrations should still prefer `v1`.
