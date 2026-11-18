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
| Test TFMs | **net10.0;net11.0** (`Tests/Directory.Build.props`); AspVersioning unit tests are net10-only. Web, OData, OpenApi.Kiota and NativeAotChecker harnesses match |
| Unit | `Tests/UnitTests/FastEndpoints`, `…/FastEndpoints.Testing`, `…/FastEndpoints.AspVersioning` (legacy `Unit.Swagger` / `Int.Swagger` are commented out in slnx; see [Harness documentation stack](#harness-documentation-stack)) |
| Integration | `Tests/IntegrationTests/FastEndpoints` (main), OpenApi, OpenApi.Kiota, OData, Agents |
| AOT | `Tests/NativeAotTests/NativeAotCheckerTests` + `NativeAot.slnx` |
| Helpers package | `Src/Testing` → `FastEndpoints.Testing` (`AppFixture`, fixtures, Bogus) |
| Main SUT | `TestHarness/Web` (`Web.Program`) |
| Other SUTs | OData, OpenApi.Kiota, Sandbox, NativeAotChecker, MessagingAotChecker |
| Sandbox tests | `TestHarness/Sandbox/Tests` (net10, scratch) is only in `Sandbox.slnx`, not the main solution or CI |

## Commands
Root `global.json` selects Microsoft.Testing.Platform for `dotnet test` (required after `xunit.v3` 4.0). SDK and dual-runtime prerequisites: [dependencies.md](dependencies.md#runtime).

```bash
# Full solution tests (run once per framework, matching GitHub publish workflow)
dotnet test FastEndpoints.slnx -c Release --framework net10.0 --verbosity minimal --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1
dotnet test FastEndpoints.slnx -c Release --framework net11.0 --verbosity minimal --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1

# Targeted
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~BindingTests
# Filters match namespaces, not folders: RPCTests/ uses `RemoteProcedureCalls` (a no-match exits with code 8, "No tests ran")
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~RemoteProcedureCalls

# Legacy Swagger search initialization (Node.js built-in runner, no npm install)
node --test Tests/UnitTests/FastEndpoints.Swagger/SwaggerSearchTests.mjs
```

CI matrix and AOT exclusions: [workflows.md](workflows.md#pack-and-publish). MTP uses `IsTestingPlatformApplication` for discovery.

AOT tests: run `dotnet test NativeAot.slnx -c Release --framework net10.0 --verbosity minimal --max-parallel-test-modules 1` and the equivalent net11 command manually. The fixture passes the matching `-f` to publish and uses `Tests/NativeAotTests/NativeAotCheckerTests/bin/Release/<TFM>/aot` for isolated binaries. Its publish fingerprint covers the target project directory, so referenced-library-only changes require an explicit `dotnet publish TestHarness/NativeAotChecker/NativeAotChecker.csproj -c Release -f <TFM> -o Tests/NativeAotTests/NativeAotCheckerTests/bin/Release/<TFM>/aot` before smoke tests to verify a fresh native binary.

Native process reuse is keyed by `TProgram`, not fixture type. For different launch environments against the same executable, use distinct marker types from its entry-point assembly (`InheritanceProgram` is an example), rather than two fixtures using `AppFixture<Program>`.

## Integration and data
- **TestBase serial + `[Priority]`:** `TestBase` / `TestBaseWithAssemblyFixture` apply method and case orderers so `[Priority]` orders `[Fact]` methods and theory rows; no consumer attribute needed. Their `DisableParallelization` flag only matters under `ParallelMode.All` (default `Collections` already runs a class serially).
- **WAF caching:** one factory per `AppFixture` type, so factory/static state persists across tests. Register doubles in `ConfigureServices`. Teardown hook: `OnCachedWafDisposedAsync()` (cached mode + `[assembly: EnableAdvancedTesting]`).
- **Sut pattern:** derive `AppFixture<Web.Program>`, override `ConfigureServices` / `SetupAsync` for clients and test doubles (`RegisterTestCommandHandler`, `RegisterTestEventHandler`, `RegisterTestEventReceivers` / `RegisterTestCommandReceivers`, etc.). Main `Sut` already registers both receiver open-generics.
- **Web harness event hubs:** in the `Testing` environment, only the main `Sut` opts in with `Testing:EnableEventHubs=true`. Other fixtures must leave them disabled so they cannot replace process-wide hub routing while RPC subscribers remain attached to the main host. `RPCTests/EventQueue.cs` exercises delivery with an unrelated host running.
- **Traits:** `[Trait("ExcludeInCiCd", "Yes")]` skips in publish/Azure pipelines (job-queue timing, some binding cases). Those tests are not a merge gate.
- **Kiota integration project:** `Int.OpenApi.Kiota` sets `IsTestingPlatformApplication=false` and `IsTestProject=false` when `CI` (GitHub) or `TF_BUILD` (Azure) is true (heavy Kiota gen; MTP keys off the former). Local `dotnet test FastEndpoints.slnx` still runs it.
- **Kiota command-mode subprocesses:** launch `typeof(Program).Assembly.Location` directly with `dotnet`, using its directory as the working directory. Pass `--urls http://127.0.0.1:0` so Kestrel atomically allocates an isolated port, even when port 5000 is occupied or TFMs run concurrently. The project reference copies the harness DLL, dependencies and runtime configuration into the matching test output. Do not derive the harness project path or TFM from `AppContext`: an IDE runner can supply its own base directory and target framework.
- The checked-in `Int.FastEndpoints` runner config disables test-collection parallelization; the other integration projects (OpenApi, Agents, OpenApi.Kiota, Swagger) enable it. Tests that mutate process-wide FastEndpoints or FluentValidation state need an explicitly nonparallel collection. Azure and GitHub publish pipelines rewrite the `FastEndpoints` runner config and pass `--max-parallel-test-modules 1`; that switch serializes test modules and leaves each module's xUnit collection policy intact.
- `Mode.WaitForAny` / `WaitForNone` offload handlers with `Task.Run`. Do not assert handler side-effects immediately after those publishes; poll, or use `WaitForAll`. For "was it published", use an event receiver (see Command/event spies).
- Unit pooled event-filter allocation checks warm and measure inside a no-GC region with `finally` cleanup, in a nonparallel collection. GC can otherwise trim warmed pool buffers and skew byte-exact comparisons.
- No external DB for the core suite; job storage tests use in-memory/test providers.
- Unit tests that host `WebApplication` + `UseFastEndpoints()` must isolate process statics ([gotchas.md](gotchas.md)). Do not add a second in-process host in `Unit.FastEndpoints` without that pattern.

## Domain regression map
| Surface | Regression location / constraint |
| --- | --- |
| Remote events | `Tests/UnitTests/FastEndpoints/EventQueueTests.*.cs`; select the partial file matching the changed surface |
| Queue wait helpers | `QueueTestSupportTests.cs`; preserve original worker faults/cancellation rather than masking them as wait timeouts |
| ACK transport deadlines | `EventQueueTests.AckTimeout.cs` and `EventQueueTests.AckWriteTimeout.cs`: real Kestrel HTTP/2 and shared `LiveDeliveryAckHost<TEvent>` |
| Remote event isolation | Distinct closed event/storage types and internal retry-delay overrides: [remote-events.md](remote-events.md#test-isolation) |
| Command dispatch / middleware | `CommandBusTests`, `CommandBusScopedMiddlewareTests` in `Unit.FastEndpoints`; discovery tests require a nonparallel collection because they replace the process-wide service resolver. AOT: `CommandsTests` in `NativeAotCheckerTests`. |
| In-process events | `EventBus*Tests`, `MessagingDiscoveryOwnerTests`, `MessagingWarmupTests`, `JobQueueWarmupTests` in `Unit.FastEndpoints`; the `Messaging` integration filter. Native AOT: `EventsTests`, `EventInheritanceTests`, and `StandaloneMessagingTests` (freshly publishes the messaging-only binary in smoke mode). Run both TFMs and preserve nonparallel/global-restoration patterns. |
| Job dedupe / reflection | Matching unit suites and `Tests/IntegrationTests/FastEndpoints/RPCTests/` |
| AOT binding / jobs / middleware | `Tests/NativeAotTests/`, including `EventStreamTests`, `ExceptionHandlerTests`, `X402Tests` and `FinancialIdempotencyTests`. The checker's X402 setup uses a fake `HttpMessageHandler` so the real facilitator client serialization runs. Run `NativeAotCheckerTests.csproj` with `-p:RunSmokeTests=false` for JIT/WAF; the default `true` publishes and tests Native AOT. |
| Financial idempotency | Unit `Financial*.cs` / `MemoryFinancialIdempotencyStoreTests.cs` (`FinancialResponseCaptureTests` uses real Kestrel); integration `FinancialIdempotencyTests/` (`Sut` for memory storage, `FinancialIdempotencyFaultSut` for storage failures); harness `TestHarness/Web/[Features]/TestCases/FinancialIdempotency/` |
| Agents schemas | `Tests/IntegrationTests/FastEndpoints.Agents/JsonSchemaBuilderTests.cs`, `McpToolSchemaRootTests.cs` |

## Native AOT gap checks

`TestHarness/NativeAotChecker` covers representative public usage, not every feature. Keep it lean: add an endpoint and test only for an untested native path the analyzer cannot verify ([blind spots](gotchas.md#operational-gotchas)), and only with owner approval.

**Per change (completion gate in [AGENTS.md](../AGENTS.md#before-finishing)):** if the change hits a blind spot, name the native path that is not exercised and say whether checker coverage may be needed. Do not add it unprompted.

**Full audit (on request):**
1. Inventory the public feature surface of each `IsAotCompatible=true` project (`Src/**/*.csproj`). Ignore `false` projects.
2. Map coverage by searching `TestHarness/NativeAotChecker` (endpoints and `Program.cs`) and `TestHarness/MessagingAotChecker` for each feature's API names. Treat OpenAPI generation as JIT-only by design (exported before publish).
3. Rank uncovered features by AOT-sensitive code: search for `UnconditionalSuppressMessage`, `Requires*`, `DynamicallyAccessedMembers`, `IsDynamicCodeSupported`, `MakeGenericType`, and direct `JsonSerializer` / `WriteAsJsonAsync` use. Also check that serialized library-owned types are registered in `FastEndpointsSerializerContext` (`Src/Library/Main/MainExtensions.cs`) or a feature context.
4. Report covered, uncovered-sensitive, and uncovered-low-risk features. Let the owner pick what to cover.
5. For each picked feature, add one small endpoint under `Endpoints/<Feature>/` and a test class under `NativeAotCheckerTests/<Feature>/`. Shared middleware goes in `Program.cs` only if it no-ops for other endpoints. Replace external services at the transport layer (for example a fake `HttpMessageHandler`), not the library client, so library serialization still runs.
6. Run JIT mode first (`-p:RunSmokeTests=false`), then Native AOT on both TFMs, republishing explicitly after library changes (see [Commands](#commands)). JIT-pass/AOT-fail isolates an AOT gap.
7. Fix small gaps with a changelog entry. Record larger ones as open gaps in gotchas and leave their tests failing until the owner decides.

## Harness documentation stack

`TestHarness/Web/Program.cs` currently selects modern OpenAPI (`AddOpenApiDocuments` + `MapOpenApi` + `MapScalarApiReference`). To test legacy NSwag:

1. Uncomment the `FastEndpoints.Swagger` project reference in `TestHarness/Web/Web.csproj`. Keep the modern OpenApi reference because shared harness code still uses it.
2. Uncomment `AddSwaggerDocuments` in `TestHarness/Web/DocumentRegistration.cs` and add `using FastEndpoints.Swagger;` there.
3. In `Program.cs`, select `AddSwaggerDocuments` instead of `AddOpenApiDocuments`, replace `MapOpenApi` and the entire `MapScalarApiReference` call with `UseSwaggerGen` inside the existing non-production guard, and replace the Scalar using with `using FastEndpoints.Swagger;`.
4. Uncomment the entire `/Legacy/Tests/` folder block in `FastEndpoints.slnx`, including both `Tests/IntegrationTests/FastEndpoints.Swagger/Int.Swagger.csproj` and `Tests/UnitTests/FastEndpoints.Swagger/Unit.Swagger.csproj`, so solution test runs include both suites.

When switching back to modern OpenAPI, reverse these changes and restore the original commented-out NSwag code, project reference, and entire solution folder block (including both test projects) exactly as they were. Do not delete the dormant NSwag setup or leave it uncommented. Preserve unrelated changes.

`Int.Swagger` requires NSwag registration, while modern `Int.OpenApi` suites require modern registration; one stack's fixture cannot validate the other.

Shared harness endpoints must supply stack-specific metadata to each generator when needed. The two packages' `AutoTagOverride` types are distinct; an override registered through the modern extension alone is invisible to NSwag.

Legacy Swagger snapshots live in `Tests/IntegrationTests/FastEndpoints.Swagger/`: release documents use `Snapshots/<TFM>/`, while unchanged release-versioning baselines remain shared in the project root. NSwag infers a `401` for the union-login response on net11 but not net10, so do not overwrite one framework's release baseline with the other's. Review document changes before regenerating with `_updateSnapshots = true` in `SwaggerDocTests.cs` and running its tests for each TFM, then restore `false` and verify `Int.Swagger` on both TFMs. Regeneration deliberately throws after writing each file; it is not a passing verification run.

## OpenAPI regression map
All suites below live in `Tests/IntegrationTests/FastEndpoints.OpenApi/` (`Int.OpenApi`). Contract details: [openapi.md](openapi.md). Check focused behavior tests as well as snapshots; preserve endpoint isolation and repeated/concurrent generation coverage.

`Fixture.GetCachedDocumentJsonAsync` reuses fixture-scoped immutable JSON only for unchanged-document inspection. Parse it separately per test. Keep `GetDocumentJsonAsync` for fresh generation and mutation-isolation checks, `GetDocumentJsonUnlockedAsync` for overlapping generation, and HTTP export generation uncached.

| Surface | Suites / focus |
| --- | --- |
| Schema / operation transformations | Select the matching `*Tests.cs` beside `Int.OpenApi.csproj`; descriptions, examples, validation, binding, tags and security have focused suites. Also use `Unit.Swagger` for shared `RemoveFromSchema` behavior. |
| XML docs without assembly location | `XmlDocLookupTests`: assembly loaded from bytes exercises the single-file fallback |
| Polymorphism | `PolymorphicOneOfTests` uses a self-hosted opt-in flag; `PolymorphicValidationTests` covers validation. |
| Framework compatibility | `ResponseTypeCompatibilityTests`, `MediaTypeCompatibilityTests`, `SseSchemaTests`; run on both TFMs |
| Host restoration | `HostGlobalStateScopeTests`: restore state even when the protected lifetime throws |

## OpenAPI self-hosted isolation
- Use a nonparallel collection for temporary hosts beside the shared Web fixture. Declare `HostGlobalStateScope` before the host so async host disposal finishes before restoring `MainExtensions.HostServiceResolver`, `SerializerConfigured`, `Config.SerOpts.Options`, and `ValidatorOptions.Global.PropertyNameResolver`. Hosts executing requests also reset/restore `Config.EpOpts.RoutePrefix`, `Filter`, and `Configurator` locally (`NullableCollectionValidationTests`). Unit-host isolation: [gotchas.md](gotchas.md#testing-and-service-lifetimes).
- Use the signed `Int.OpenApi` friend boundary for typed `ValidationSchemaApplier` / `ValidationRuleCatalog.DefaultRules` access; private live-validator invocation remains reflective.

## OpenAPI snapshots
- Goldens: `Tests/IntegrationTests/FastEndpoints.OpenApi/Snapshots/<TFM>/` (`net10.0` and `net11.0`) contains `release-*.http` and `release-*.json` (including `release-versioning-*`). `SnapshotFiles.ForFramework` selects the matching baseline for reading and regeneration.
- Regenerate goldens separately with `--framework net10.0` and `--framework net11.0`. OpenAPI 11 defaults to OpenAPI 3.2, documents QUERY operations and has different framework schema/nullability and server URL output, so its JSON and HTTP baselines are separate rather than normalizing away differences. Generated files must come from the snapshot tests, not manual edits.
- Regeneration: set `_updateSnapshots = true` in `HttpSnapshotTests.cs` (HTTP) or `SnapshotTests.cs` (JSON), run the matching filter once per TFM, reset the flag to `false`, then rerun both to verify. Example for HTTP on net10:
  ```bash
  dotnet test Tests/IntegrationTests/FastEndpoints.OpenApi/Int.OpenApi.csproj --framework net10.0 --filter FullyQualifiedName~HttpSnapshotTests
  ```
  Repeat with `--framework net11.0`; JSON uses `FullyQualifiedName~SnapshotTests` (which also matches the HTTP class).

## Command/event spies
- **API:** `RegisterTestEventReceivers()` / `RegisterTestCommandReceivers()`, then `GetTestEventReceiver<T>()` / `GetTestCommandReceiver<T>()`. Assert with `WaitForMatchAsync(match, timeoutSeconds = 2)`.
- **Use when** an HTTP, RPC, or dispatcher path should have published an event or executed a command, and the assertion is receipt (payload/predicate), not handler logic. Prefer this over capturing statics or extra fake handlers.
- **Do not use when** proving `RegisterTestCommandHandler` / `RegisterTestEventHandler` substitution; asserting handler completion or mutations; asserting job *successful* completion (receiver sees `ExecuteAsync` start, including throws/retries); asserting event-hub *subscriber* delivery (hub receiver is publisher-side); client-stream and duplex-stream RPC (no hook); NativeAot published process (no test DI).
- Match on a unique value (`Guid.NewGuid()`). Receivers are fixture singletons and never clear; `_ => true` can match earlier tests. Capture means publish/execute start, not completion.
- Examples: `Tests/IntegrationTests/FastEndpoints/MessagingTests/{CommandBusTests,EventBusTests}.cs`.

## Expectations
- New public behavior: unit tests when pure logic; integration tests against `TestHarness/Web` (or domain harness) when pipeline/HTTP involved.
- Prefer endpoint-typed client extensions over magic strings.
- Generator behavior: unit tests reference Generator project and harness where needed (`Unit.FastEndpoints` references Generator + Web).

## Sources
- `Tests/Directory.Build.props`
- `Src/Library/Testing/TestingExtensions.cs`
- `Src/Testing/AppFixture.Waf.cs`
- `Tests/IntegrationTests/FastEndpoints/Sut.cs`
- `.github/workflows/publish-to-nuget.yml`
