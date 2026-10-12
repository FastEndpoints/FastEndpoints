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

# Targeted
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~BindingTests
```

Azure uses the task project glob `Tests/**/*.csproj`; use the solution command above for a portable shell invocation. MTP uses `IsTestingPlatformApplication` for discovery; exit 8 means zero tests ran.

AOT tests: use `NativeAot.slnx` (publish workflow currently has AOT test step commented out; re-check before assuming CI runs AOT). The fixture's publish fingerprint covers the target project directory, so referenced-library-only changes require an explicit `dotnet publish TestHarness/NativeAotChecker/NativeAotChecker.csproj -c Release -o Tests/NativeAotTests/NativeAotCheckerTests/bin/aot/NativeAotChecker` before smoke tests to verify a fresh native binary.

## Integration and data
- **TestBase serial + `[Priority]`:** `TestBase` / `TestBaseWithAssemblyFixture` apply `[TestClass(DisableParallelization = true)]`, `[TestCaseOrderer]`, and `[TestMethodOrderer]`. xunit.v3 4 default parallel mode is `Collections` (same class already serial). The `TestClass` flag is ignored in that mode and only applies if a project enables `ParallelMode.All`. Method + case orderers restore `[Priority]` across `[Fact]` methods and theory rows. No consumer attribute needed.
- **WAF caching:** one factory per `AppFixture` type. Teardown hook is `OnCachedWafDisposedAsync()` (cached mode + `[assembly: EnableAdvancedTesting]`). Leak traps: [gotchas.md](gotchas.md).
- **Sut pattern:** derive `AppFixture<Web.Program>`, override `ConfigureServices` / `SetupAsync` for clients and test doubles (`RegisterTestCommandHandler`, `RegisterTestEventHandler`, `RegisterTestEventReceivers` / `RegisterTestCommandReceivers`, etc.). Main `Sut` already registers both receiver open-generics.
- **Auth clients:** Admin/Customer JWT obtained via login endpoints in `Sut.SetupAsync`.
- **Traits:** `[Trait("ExcludeInCiCd", "Yes")]` skips in publish/Azure pipelines (job-queue timing, some binding cases). Those tests are not a merge gate.
- **Kiota integration project:** `Int.OpenApi.Kiota` sets `IsTestingPlatformApplication=false` and `IsTestProject=false` when `CI` (GitHub) or `TF_BUILD` (Azure) is true (heavy Kiota gen; MTP keys off the former). Local `dotnet test FastEndpoints.slnx` still runs it.
- Checked-in `FastEndpoints` and `FastEndpoints.OpenApi` runner configs enable test-collection parallelization; `FastEndpoints.Agents` disables it. Tests that mutate process-wide FastEndpoints or FluentValidation state need an explicitly nonparallel collection. Azure and GitHub publish pipelines rewrite the `FastEndpoints` runner config and pass `--max-parallel-test-modules 1`; that switch serializes test modules and leaves each module's xUnit collection policy intact.
- `Mode.WaitForAny` / `WaitForNone` offload handlers with `Task.Run`. Do not assert handler side-effects immediately after those publishes; poll, or use `WaitForAll`. For "was it published", use an event receiver (see Command/event spies).
- No external DB for the core suite; job storage tests use in-memory/test providers.
- Financial HTTP idempotency: real Kestrel response-lifecycle tests in `FinancialResponseCaptureTests.cs` cover upstream/downstream callback settlement, LIFO ordering, exceptions, client cancellation, empty responses, compression, upstream-initialized cookie append/delete (with and without cookie policy), replay cookie exclusions, and nonfinancial callback/completion behavior; unit tests in `Tests/UnitTests/FastEndpoints/Financial*.cs` / `MemoryFinancialIdempotencyStoreTests.cs`; harness endpoints in `TestHarness/Web/[Features]/TestCases/FinancialIdempotency/`; integration in `Tests/IntegrationTests/FastEndpoints/FinancialIdempotencyTests/` (`Sut` for the memory path, `FinancialIdempotencyFaultSut` for in-flight/`Complete` failure fakes).
- Unit tests that host `WebApplication` + `UseFastEndpoints()` must isolate process statics ([gotchas.md](gotchas.md)). Do not add a second in-process host in `Unit.FastEndpoints` without that pattern.

## Domain regression map
| Surface | Regression location / constraint |
| --- | --- |
| Remote events | `Tests/UnitTests/FastEndpoints/EventQueueTests.*.cs`: retrieval/idle, deserialization, delivery ACK, store retries/expiry, retention, lifecycle and receive supervision |
| Queue wait helpers | `QueueTestSupportTests.cs`: successful completion, pending deadlines, worker faults (including `TimeoutException`), worker cancellation and wait cancellation. `WaitForCompletion` and `CompletesWithin` re-await completed tasks after a caught timeout so original failures remain visible. |
| ACK transport deadlines | `EventQueueTests.AckTimeout.cs` and `EventQueueTests.AckWriteTimeout.cs`: real Kestrel HTTP/2 and shared `LiveDeliveryAckHost<TEvent>` |
| Remote event isolation | Distinct closed event/storage types and internal retry-delay overrides: [remote-events.md](remote-events.md#test-isolation) |
| Void command middleware | `CommandBusTests` in `Unit.FastEndpoints` runs in a non-parallel collection because discovery/dispatch tests replace the process-wide service resolver. Covers both no-result handler interfaces through `AddMessaging` discovery, middleware and test-handler substitution, plus direct DI, helper and mixed registrations, closed/open middleware ordering, repeated dispatch, scoped and transient dependencies. `CommandsTests` in `NativeAotCheckerTests` verifies the closed-registration AOT path. |
| Job dedupe / reflection | Matching unit suites and `Tests/IntegrationTests/FastEndpoints/RPCTests/` |
| AOT binding / jobs | `Tests/NativeAotTests/`; `JobsTests.Void_Jobs_Use_Public_Result_Type_With_Source_Generated_Storage` covers success, bounded storage attempts, and failure recovery with a `Void`/`string`-only serializer context. Run `NativeAotCheckerTests.csproj` with `-p:RunSmokeTests=false` for JIT/WAF; the default `true` publishes and tests Native AOT. |
| Agents schemas | `Tests/IntegrationTests/FastEndpoints.Agents/JsonSchemaBuilderTests.cs`, `McpToolSchemaRootTests.cs` |
| OpenAPI XML reference descriptions | `XmlDocSchemaTransformerTests` in `Int.OpenApi` (self-hosted HTTP, repeated document generation, object/collection/enum references, nullable collections, inline converter descriptions, component summary isolation, and multi-line summary indentation with nested-only lists, indented code blocks, and Markdown hard breaks). `OperationSchemaHelpersTests.schema_visitor_materializes_nested_descriptions_without_following_references` covers in-place traversal of all child categories and the component-reference boundary. `RequestDescriptionOverrideTests` combines type/property XML summaries with endpoint overrides equal to the type summary, equal to the property summary, and custom text; asserts endpoint isolation, unchanged request-schema reuse, child component reuse, and repeated generation. Contract: [gotchas.md](gotchas.md) |
| OpenAPI XML docs in single-file apps | `XmlDocLookupTests` in `Int.OpenApi` (assembly loaded from bytes, so it has no location) |
| OpenAPI enums / examples | `EnumSchemaTransformerTests`, `NullableCollectionSchemaTests`, `OperationTransformerEdgeCaseTests`, `OperationSchemaHelpersTests` in `Int.OpenApi` |
| OpenAPI validation rule mapping | `ValidationSchemaTransformerTests` and `NullableCollectionValidationTests` in `Int.OpenApi` (conditional presence rules, conditional collection pattern/length/comparison/range mappings across chained/outer sync/async conditions, per-component `CurrentValidator` preservation, collection rule paths, nullable-wrapper resolution in both branch orders, referenced-array/item isolation, multi-type union guards, and self-hosted HTTP schema/runtime agreement across operations and repeated generation). Contract: [gotchas.md](gotchas.md) |
| OpenAPI nested group tags | `OperationTagTests` and `NestedGroupTagTests` in `Int.OpenApi`. Contract: [gotchas.md](gotchas.md) |
| OpenAPI polymorphic `oneOf` | `PolymorphicOneOfTests` in `Int.OpenApi` (self-hosted, flag off the shared Web fixture). Contract: [gotchas.md](gotchas.md) |

## OpenAPI self-hosted isolation
- `HostGlobalStateScope` in `Int.OpenApi` snapshots and restores the host service resolver, serializer-configured flag, serializer options, and FluentValidation property-name resolver. Declare it before the temporary host so async host disposal completes before restoration. Endpoint-option resets remain local to each test.
- Validation helpers use the signed `Int.OpenApi` friend boundary for typed `ValidationSchemaApplier` construction and direct `ValidationRuleCatalog.DefaultRules` access. Private live-validator invocation remains reflective. `ValidationSchemaTransformerTests` covers live child includes and self-includes with naming-policy conversion, plus two-validator include cycles, sibling-branch type reuse and include order, and successful/failing include providers through both cached and live-child entrypoints.
- `HostGlobalStateScopeTests` covers restoration when the protected lifetime throws. `ValidationSchemaTransformerTests.scalar_item_and_child_validator_rules_reuse_the_same_referenced_item_variant` covers item-level presence and child-property constraints sharing one localized item variant in both rule orders, with shared components preserved.
- `ValidationSchemaTransformerTests` directly exercises `ChildValidatorResolver` scope laziness, provider reuse, disposal, active-path recursion rejection, and guard cleanup after an application callback throws. Child-recursion coverage stays separate from include-cycle coverage.

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
