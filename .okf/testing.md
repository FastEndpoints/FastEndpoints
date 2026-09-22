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
| Framework | **xunit.v3** (MTP v2, no VSTest adapter), Shouldly, FakeItEasy |
| Test TFM | **net10.0** (`Tests/Directory.Build.props`) |
| Unit | `Tests/UnitTests/FastEndpoints`, `…/FastEndpoints.Testing`, `…/FastEndpoints.AspVersioning` (+ legacy Swagger unit commented in slnx) |
| Integration | `Tests/IntegrationTests/FastEndpoints` (main), OpenApi, OpenApi.Kiota, OData, Agents |
| AOT | `Tests/NativeAotTests/NativeAotCheckerTests` + `NativeAot.slnx` |
| Helpers package | `Src/Testing` → `FastEndpoints.Testing` (`AppFixture`, fixtures, Bogus) |
| Main SUT | `TestHarness/Web` (`Web.Program`) |
| Other SUTs | OData, OpenApi.Kiota, Sandbox, NativeAotChecker |

Integration projects reference harness + `FastEndpoints.Testing` + often remote messaging testing helpers.

## Commands
Root `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }` so `dotnet test` uses MTP on the .NET 10 SDK (required after `xunit.v3` 4.0).

```bash
# Full solution tests (matches GitHub publish workflow)
dotnet test FastEndpoints.slnx -c Release --verbosity minimal --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1

# By tree (Azure pipeline workingDirectory Tests)
dotnet test Tests/**/*.csproj -c Release --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1

# Targeted
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~BindingTests
```

AOT tests: use `NativeAot.slnx` (publish workflow currently has AOT test step commented out; re-check before assuming CI runs AOT).

## Integration and data
- **TestBase serial + `[Priority]`:** `TestBase` / `TestBaseWithAssemblyFixture` apply `[TestClass(DisableParallelization = true)]`, `[TestCaseOrderer]`, and `[TestMethodOrderer]`. xunit.v3 4 default parallel mode is `Collections` (same class already serial). The `TestClass` flag is ignored in that mode and only applies if a project enables `ParallelMode.All`. Method + case orderers restore `[Priority]` across `[Fact]` methods and theory rows. No consumer attribute needed.
- **WAF caching:** one factory per `AppFixture` type. Teardown hook is `OnCachedWafDisposedAsync()` (cached mode + `[assembly: EnableAdvancedTesting]`). Leak traps: [gotchas.md](gotchas.md).
- **Sut pattern:** derive `AppFixture<Web.Program>`, override `ConfigureServices` / `SetupAsync` for clients and test doubles (`RegisterTestCommandHandler`, `RegisterTestEventHandler`, `RegisterTestEventReceivers` / `RegisterTestCommandReceivers`, etc.). Main `Sut` already registers both receiver open-generics.
- **Auth clients:** Admin/Customer JWT obtained via login endpoints in `Sut.SetupAsync`.
- **Traits:** `[Trait("ExcludeInCiCd", "Yes")]` skips in publish/Azure pipelines (job-queue timing, some binding cases). Those tests are not a merge gate.
- **Kiota integration project:** `Int.OpenApi.Kiota` sets `IsTestingPlatformApplication=false` and `IsTestProject=false` when `CI` (GitHub) or `TF_BUILD` (Azure) is true (heavy Kiota gen; MTP keys off the former). Local `dotnet test FastEndpoints.slnx` still runs it.
- Integration runners for `FastEndpoints`, `FastEndpoints.OpenApi`, and `FastEndpoints.Agents` disable test-collection parallelization (process-wide FastEndpoints state). Azure and GitHub publish pipelines also rewrite the `FastEndpoints` runner config and pass `--max-parallel-test-modules 1` so test assemblies do not starve each other on 2-core runners.
- `Mode.WaitForAny` / `WaitForNone` offload handlers with `Task.Run`. Do not assert handler side-effects immediately after those publishes; poll, or use `WaitForAll`. For "was it published", use an event receiver (see Command/event spies).
- No external DB for the core suite; job storage tests use in-memory/test providers.
- Job-queue idempotency, gRPC reflection, and AOT binding/jobs live under the matching `Tests/UnitTests`, `Tests/IntegrationTests/FastEndpoints/RPCTests`, and `Tests/NativeAotTests` folders. Do not stand up a second in-process event hub with default storage types (see [gotchas.md](gotchas.md)).
- Financial HTTP idempotency: real Kestrel response-lifecycle tests in `FinancialResponseCaptureTests.cs`; unit tests in `Tests/UnitTests/FastEndpoints/Financial*.cs` / `MemoryFinancialIdempotencyStoreTests.cs`; harness endpoints in `TestHarness/Web/[Features]/TestCases/FinancialIdempotency/`; integration in `Tests/IntegrationTests/FastEndpoints/FinancialIdempotencyTests/` (`Sut` for the memory path, `FinancialIdempotencyFaultSut` for in-flight/`Complete` failure fakes).
- Unit tests that host `WebApplication` + `UseFastEndpoints()` must isolate process statics ([gotchas.md](gotchas.md)). Do not add a second in-process host in `Unit.FastEndpoints` without that pattern.

## OpenAPI snapshots
- Goldens: `Tests/IntegrationTests/FastEndpoints.OpenApi/release-*.http` and `release-*.json` (plus `release-versioning-*`).
- Walker/export/versioning behavior is covered by focused tests in that project, not snapshots alone. Export mode keys live on internal `OpenApiExportMode`; public `IsExportMode` / `IsNotExportMode` (+ per-format wrappers) on `IHost` / `IHostApplicationBuilder`.
- To regenerate `.http` goldens: set `_updateSnapshots = true` in `HttpSnapshotTests.cs`, run  
  `dotnet test Tests/IntegrationTests/FastEndpoints.OpenApi/Int.OpenApi.csproj --filter FullyQualifiedName~HttpSnapshotTests`,  
  set `_updateSnapshots = false`, re-run the same filter. JSON goldens use the same flag in `SnapshotTests.cs`.

## Command/event spies
- **API:** `RegisterTestEventReceivers()` / `RegisterTestCommandReceivers()`, then `GetTestEventReceiver<T>()` / `GetTestCommandReceiver<T>()`. Assert with `WaitForMatchAsync(match, timeoutSeconds = 2)`.
- **Use when** an HTTP, RPC, or dispatcher path should have published an event or executed a command, and the assertion is receipt (payload/predicate), not handler logic. Prefer this over capturing statics or extra fake handlers.
- **Do not use when** proving `RegisterTestCommandHandler` / `RegisterTestEventHandler` substitution; asserting handler completion or mutations; asserting job *successful* completion (receiver sees `ExecuteAsync` start, including throws/retries); asserting event-hub *subscriber* delivery (hub receiver is publisher-side); client-stream RPC (no hook); NativeAot published process (no test DI).
- Match on a unique value (`Guid.NewGuid()`). Receivers are fixture singletons and never clear. A loose `_ => true` matches leftovers from earlier tests on the same `Sut`. Capture is publish/execute start (`EventBus.Execute`, `CommandHandlerExecutor` including streams, RPC unary/void/server-stream, `EventHub.BroadcastEventTask`), not handler finished and not job completed-after-retry.
- Worked examples: `Tests/IntegrationTests/FastEndpoints/MessagingTests/CommandBusTests.cs` (`Test_Command_Receiver_Receives_Executed_Command`), `…/EventBusTests.cs` (`Test_Event_Receiver_Receives_Event`).

## Expectations
- New public behavior: unit tests when pure logic; integration tests against `TestHarness/Web` (or domain harness) when pipeline/HTTP involved.
- Prefer endpoint-typed client extensions over magic strings.
- Generator behavior: unit tests reference Generator project and harness where needed (`Unit.FastEndpoints` references Generator + Web).
- Keep assemblies signed consistently when using `InternalsVisibleTo` (public key in props).

## Sources
- `Tests/Directory.Build.props`
- `Src/Library/Testing/TestingExtensions.cs`
- `Src/Testing/AppFixture.Waf.cs`
- `Tests/IntegrationTests/FastEndpoints/Sut.cs`
- `.github/workflows/publish-to-nuget.yml`
