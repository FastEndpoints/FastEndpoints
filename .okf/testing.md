---
type: Playbook
title: Testing
description: xUnit v3 layout, harnesses, AppFixture, and CI filter conventions.
tags: [test]
---

# Testing

## Frameworks and layout
| Piece | Detail |
| --- | --- |
| Framework | **xunit.v3**, Shouldly, FakeItEasy |
| Test TFM | **net10.0** (`Tests/Directory.Build.props`) |
| Unit | `Tests/UnitTests/FastEndpoints`, `…/FastEndpoints.Testing` (+ legacy Swagger unit commented in slnx) |
| Integration | `Tests/IntegrationTests/FastEndpoints` (main), OpenApi, OpenApi.Kiota, OData, Agents |
| AOT | `Tests/NativeAotTests/NativeAotCheckerTests` + `NativeAot.slnx` |
| Helpers package | `Src/Testing` → `FastEndpoints.Testing` (`AppFixture`, fixtures, Bogus) |
| Main SUT | `TestHarness/Web` (`Web.Program`) |
| Other SUTs | OData, OpenApi.Kiota, Sandbox, NativeAotChecker |

Integration projects reference harness + `FastEndpoints.Testing` + often remote messaging testing helpers.

## Commands
```bash
# Full solution tests (matches GitHub publish workflow)
dotnet test FastEndpoints.slnx -c Release --verbosity minimal --filter "ExcludeInCiCd!=Yes"

# By tree (Azure pipeline workingDirectory Tests)
dotnet test Tests/**/*.csproj -c Release --filter "ExcludeInCiCd!=Yes"

# Targeted
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~BindingTests
```

AOT tests: use `NativeAot.slnx` (publish workflow currently has AOT test step commented out; re-check before assuming CI runs AOT).

## Integration and data
- **WAF caching:** `AppFixture` caches one factory per fixture type for concurrency; override `OnCachedWafDisposedAsync()` for one-shot teardown of the shared factory (cached mode + `[assembly: EnableAdvancedTesting]`).
- **Sut pattern:** derive `AppFixture<Web.Program>`, override `ConfigureServices` / `SetupAsync` for clients and test doubles (`RegisterTestCommandHandler`, event receivers, etc.).
- **Auth clients:** e.g. Admin/Customer JWT obtained via login endpoints in `Sut.SetupAsync`.
- **Traits:** `[Trait("ExcludeInCiCd", "Yes")]` skips in CI (job queue timing, some binding/Kiota cases).
- Integration xunit runner: `Tests/IntegrationTests/FastEndpoints/xunit.runner.json` disables test-collection parallelization because several tests and fixtures mutate process-wide FastEndpoints state. Azure and GitHub publish pipelines also rewrite the configuration to keep parallelization disabled.
- No external DB required for core suite; job storage tests use in-memory/test providers in harness/tests.
- Job queue idempotency unit coverage: `Tests/UnitTests/FastEndpoints/JobQueueTests.Idempotency.cs` (+ fixtures in `JobQueueTests.Fixtures.cs`). Harness `TestStorageProvider` implements `IHasIdempotencyKey` and throws `DuplicateJobException`.
- gRPC reflection coverage: `Tests/IntegrationTests/FastEndpoints/RPCTests/GrpcReflection.cs` (+ same-simple-name fixtures in `GrpcReflection.Fixtures.cs`). Descriptor generation is asserted directly; only the list/describe and protobuf round-trip tests stand up a server. Do not add a live event hub there; see gotchas.
- NativeAotChecker AOT/harness coverage: `UseJobQueues(... IdempotencyKeyFor<IdempotentEchoCommand>(c => c.OrderId))`, endpoint `job-queue/idempotent`, test `Tests/NativeAotTests/NativeAotCheckerTests/Jobs/JobQueueIdempotencyTests.cs`.

## OpenAPI `.http` export snapshots
- Goldens: `Tests/IntegrationTests/FastEndpoints.OpenApi/release-*.http` compared by `HttpSnapshotTests`.
- Focused walker/security coverage: `HttpFileExporterTests` + `HttpExportRegressionTests` (not snapshots alone).
- Multi-format export orchestration: `OpenApiExporterTests` (in-process via `ExportRequestedFormatsAsync`, fake `IOpenApiDocumentProvider`; no `Environment.Exit`). Mode keys/predicates owned by internal `OpenApiExportMode` (`IsJson`/`IsHttp`/`IsAny`); public `IsExportMode`/`IsNotExportMode` (+ per-format wrappers) on `IHost`/`IHostApplicationBuilder`.
- To regenerate goldens: set `_updateSnapshots = true` in `HttpSnapshotTests.cs`, run  
  `dotnet test Tests/IntegrationTests/FastEndpoints.OpenApi/Int.OpenApi.csproj --filter FullyQualifiedName~HttpSnapshotTests`,  
  set `_updateSnapshots = false`, re-run the same filter (must pass 7/7). Spot-check admin login, inventory create, dual-child-address bodies.

## Expectations
- New public behavior: unit tests when pure logic; integration tests against `TestHarness/Web` (or domain harness) when pipeline/HTTP involved.
- Prefer endpoint-typed client extensions over magic strings.
- Generator behavior: unit tests reference Generator project and harness where needed (`Unit.FastEndpoints` references Generator + Web).
- Keep assemblies signed consistently when using `InternalsVisibleTo` (public key in props).

## Sources
- `Tests/Directory.Build.props`
- `Src/Testing/AppFixture.Waf.cs`
- `Tests/IntegrationTests/FastEndpoints/Sut.cs`
- `Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj`
- `.github/workflows/publish-to-nuget.yml`
