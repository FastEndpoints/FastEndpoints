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
| Test TFMs | **net10.0;net11.0** (`Tests/Directory.Build.props`); Web, OData, OpenApi.Kiota and NativeAotChecker harnesses match |
| Unit | `Tests/UnitTests/FastEndpoints`, `…/FastEndpoints.Testing`, `…/FastEndpoints.AspVersioning` (+ legacy Swagger unit commented in slnx) |
| Integration | `Tests/IntegrationTests/FastEndpoints` (main), OpenApi, OpenApi.Kiota, OData, Agents |
| AOT | `Tests/NativeAotTests/NativeAotCheckerTests` + `NativeAot.slnx` |
| Helpers package | `Src/Testing` → `FastEndpoints.Testing` (`AppFixture`, fixtures, Bogus) |
| Main SUT | `TestHarness/Web` (`Web.Program`) |
| Other SUTs | OData, OpenApi.Kiota, Sandbox, NativeAotChecker |

Integration projects reference harness + `FastEndpoints.Testing` + often remote messaging testing helpers.

## Commands
Root `global.json` pins the .NET 11 RC2 SDK and sets `"test": { "runner": "Microsoft.Testing.Platform" }` so `dotnet test` uses MTP (required after `xunit.v3` 4.0). Install both .NET 10 and .NET 11 runtimes to run the matrix.

```bash
# Full solution tests (run once per framework, matching GitHub publish workflow)
dotnet test FastEndpoints.slnx -c Release --framework net10.0 --verbosity minimal --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1
dotnet test FastEndpoints.slnx -c Release --framework net11.0 --verbosity minimal --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1

# Targeted
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~BindingTests

# Legacy Swagger search initialization (Node.js built-in runner, no npm install)
node --test Tests/UnitTests/FastEndpoints.Swagger/SwaggerSearchTests.mjs
```

Azure installs only the SDK from `global.json` and runs net11.0 tests using the task project glob `Tests/**/*.csproj`, including Native AOT tests. GitHub publish runs the primary solution on both TFMs but leaves its separate AOT steps disabled. Use the solution commands above for portable shell invocations. MTP uses `IsTestingPlatformApplication` for discovery; exit 8 means zero tests ran.

AOT tests: run `dotnet test NativeAot.slnx -c Release --framework net10.0 --verbosity minimal --max-parallel-test-modules 1` and the equivalent net11 command manually. GitHub publish has these steps commented out to reduce CI duration. The fixture passes the matching `-f` to publish and uses `Tests/NativeAotTests/NativeAotCheckerTests/bin/Release/<TFM>/aot` for isolated binaries. Its publish fingerprint covers the target project directory, so referenced-library-only changes require an explicit `dotnet publish TestHarness/NativeAotChecker/NativeAotChecker.csproj -c Release -f <TFM> -o Tests/NativeAotTests/NativeAotCheckerTests/bin/Release/<TFM>/aot` before smoke tests to verify a fresh native binary.

## Integration and data
- **TestBase serial + `[Priority]`:** `TestBase` / `TestBaseWithAssemblyFixture` apply `[TestClass(DisableParallelization = true)]`, `[TestCaseOrderer]`, and `[TestMethodOrderer]`. xunit.v3 4 default parallel mode is `Collections` (same class already serial). The `TestClass` flag is ignored in that mode and only applies if a project enables `ParallelMode.All`. Method + case orderers restore `[Priority]` across `[Fact]` methods and theory rows. No consumer attribute needed.
- **WAF caching:** one factory per `AppFixture` type. Teardown hook is `OnCachedWafDisposedAsync()` (cached mode + `[assembly: EnableAdvancedTesting]`). Leak traps: [gotchas.md](gotchas.md).
- **Sut pattern:** derive `AppFixture<Web.Program>`, override `ConfigureServices` / `SetupAsync` for clients and test doubles (`RegisterTestCommandHandler`, `RegisterTestEventHandler`, `RegisterTestEventReceivers` / `RegisterTestCommandReceivers`, etc.). Main `Sut` already registers both receiver open-generics.
- **Auth clients:** Admin/Customer JWT obtained via login endpoints in `Sut.SetupAsync`.
- **Traits:** `[Trait("ExcludeInCiCd", "Yes")]` skips in publish/Azure pipelines (job-queue timing, some binding cases). Those tests are not a merge gate.
- **Kiota integration project:** `Int.OpenApi.Kiota` sets `IsTestingPlatformApplication=false` and `IsTestProject=false` when `CI` (GitHub) or `TF_BUILD` (Azure) is true (heavy Kiota gen; MTP keys off the former). Local `dotnet test FastEndpoints.slnx` still runs it.
- **Kiota command-mode subprocesses:** launch `typeof(Program).Assembly.Location` directly with `dotnet`, using its directory as the working directory. Pass `--urls http://127.0.0.1:0` so Kestrel atomically allocates an isolated port, even when port 5000 is occupied or TFMs run concurrently. The project reference copies the harness DLL, dependencies and runtime configuration into the matching test output. Do not derive the harness project path or TFM from `AppContext`: an IDE runner can supply its own base directory and target framework.
- Checked-in `FastEndpoints` and `FastEndpoints.OpenApi` runner configs enable test-collection parallelization; `FastEndpoints.Agents` disables it. Tests that mutate process-wide FastEndpoints or FluentValidation state need an explicitly nonparallel collection. Azure and GitHub publish pipelines rewrite the `FastEndpoints` runner config and pass `--max-parallel-test-modules 1`; that switch serializes test modules and leaves each module's xUnit collection policy intact.
- `Mode.WaitForAny` / `WaitForNone` offload handlers with `Task.Run`. Do not assert handler side-effects immediately after those publishes; poll, or use `WaitForAll`. For "was it published", use an event receiver (see Command/event spies).
- No external DB for the core suite; job storage tests use in-memory/test providers.
- Financial HTTP idempotency: `FinancialResponseCaptureTests.cs` uses real Kestrel for response-lifecycle behavior. Unit tests: `Tests/UnitTests/FastEndpoints/Financial*.cs` / `MemoryFinancialIdempotencyStoreTests.cs`. Harness: `TestHarness/Web/[Features]/TestCases/FinancialIdempotency/`. Integration: `Tests/IntegrationTests/FastEndpoints/FinancialIdempotencyTests/` (`Sut` for memory storage, `FinancialIdempotencyFaultSut` for storage failures).
- Unit tests that host `WebApplication` + `UseFastEndpoints()` must isolate process statics ([gotchas.md](gotchas.md)). Do not add a second in-process host in `Unit.FastEndpoints` without that pattern.

## Domain regression map
| Surface | Regression location / constraint |
| --- | --- |
| Remote events | `Tests/UnitTests/FastEndpoints/EventQueueTests.*.cs`: retrieval/idle, deserialization, delivery ACK, store retries/expiry, retention, lifecycle and receive supervision |
| Queue wait helpers | `QueueTestSupportTests.cs`; preserve original worker faults/cancellation rather than masking them as wait timeouts |
| ACK transport deadlines | `EventQueueTests.AckTimeout.cs` and `EventQueueTests.AckWriteTimeout.cs`: real Kestrel HTTP/2 and shared `LiveDeliveryAckHost<TEvent>` |
| Remote event isolation | Distinct closed event/storage types and internal retry-delay overrides: [remote-events.md](remote-events.md#test-isolation) |
| Command dispatch / middleware | `CommandBusTests`, `CommandBusScopedMiddlewareTests` in `Unit.FastEndpoints`; discovery tests require a nonparallel collection because they replace the process-wide service resolver. AOT: `CommandsTests` in `NativeAotCheckerTests`. |
| Job dedupe / reflection | Matching unit suites and `Tests/IntegrationTests/FastEndpoints/RPCTests/` |
| AOT binding / jobs | `Tests/NativeAotTests/`, including `JobsTests.Void_Jobs_Use_Public_Result_Type_With_Source_Generated_Storage`. Run `NativeAotCheckerTests.csproj` with `-p:RunSmokeTests=false` for JIT/WAF; the default `true` publishes and tests Native AOT. |
| Agents schemas | `Tests/IntegrationTests/FastEndpoints.Agents/JsonSchemaBuilderTests.cs`, `McpToolSchemaRootTests.cs` |

## OpenAPI regression map
All suites below live in `Tests/IntegrationTests/FastEndpoints.OpenApi/` (`Int.OpenApi`). Contract details: [openapi.md](openapi.md). Check focused behavior tests as well as snapshots; preserve endpoint isolation and repeated/concurrent generation coverage.

| Surface | Suites / focus |
| --- | --- |
| XML descriptions / examples | `XmlDocSchemaTransformerTests`, `XmlDocValidatedRequestTests`, `RequestDescriptionOverrideTests` |
| XML docs without assembly location | `XmlDocLookupTests`: assembly loaded from bytes exercises the single-file fallback |
| Enums / examples / reference cloning | `EnumSchemaTransformerTests`, `NullableCollectionSchemaTests`, `OperationTransformerEdgeCaseTests`, `OperationSchemaHelpersTests`, `HttpFileExporterTests` |
| Validation / collections | `ValidationSchemaTransformerTests`, `NullableCollectionValidationTests`, `ValidationRuleMappingTests` |
| Parameters / binding sources | `ParameterAliasValidationTests`, `ValidationBindingSourceTests`, `ValidationParameterTransformerTests`, `ConditionalParameterValidationTests`, `QueryParameterVisibilityTests` (also `Unit.Swagger` for `RemoveFromSchema`) |
| Tags / security | `OperationTagTests`, `NestedGroupTagTests`, `OperationSecurityTests` |
| Polymorphic `oneOf` | `PolymorphicOneOfTests`; self-hosted opt-in flag |
| Polymorphic validation | `PolymorphicValidationTests` |
| Framework compatibility | `ResponseTypeCompatibilityTests`, `MediaTypeCompatibilityTests`; run on both TFMs |
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
