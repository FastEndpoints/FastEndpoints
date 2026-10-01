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
- Hub retrieval tests (`EventQueueTests.Retrieval.cs`) exercise ordinary and ACK dispatchers with two failures, a successful delivery, another failure and recovery, followed by blocked-fetch cancellation. They assert callback counts `1, 2, 1`, throwing-callback isolation, query eligibility, protocol-specific limits, completion and connection/app cancellation. These tests pass a zero retrieval retry delay through the internal dispatcher override and use distinct closed event/storage types per protocol. Production callers retain the default five-second storage retry delay. Focused `EventQueueTests.Idle.cs` helper tests cover connection/app cancellation during an empty-batch wait, disposed-semaphore shutdown, and signal-triggered refetch with residual-release draining.
- Hub deserialization tests (`EventQueueTests.Deserialization.cs`) cover recovery within three attempts, cancellation during retry, and terminal callback ordering/continuation for ordinary and ACK dispatchers, including throwing receivers. Helper-only tests, terminal failure ordering/continuation, callback-cancellation, and unreadable ACK head-row tests use zero retry delay through per-call internal dispatcher overrides. The unreadable head-row test starts the dispatcher directly; subscriber registration is covered by the other hub/session tests. An in-memory batch regression cancels during the second record's deserialization retry and checks ordered current-record/suffix requeue with `CancellationToken.None`, no suffix reads, and connection release. Production dispatchers retain one-second retry delays.
- Delivery-ack retention tests use the side-effect-free `EventSubscriberRetentionPolicy<TStorageRecord>.PurgeMatch` predicate (ordinary record types also have direct predicate coverage), repeated reconnects during hub completion failures, mixed ordinary/ACK rows, execution expiry independent of retention, eventual cleanup, allowance validation (including records without ACK retention capability), retained-key deadline calculation, and missing replay deadline rejection. The repeated-reconnect test cancels the active call inside `OnMarkFailure`, before the hub enters its five-second storage retry delay.
- ACK write-deadline regressions (`EventQueueTests.AckWriteTimeout.cs`) cover blocked-writer cancellation cleanup before ownership release, separate post-write ACK budgets, connection/app cancellation, and real Kestrel HTTP/2 unread 32 MiB deliveries with timeout/expiry, pending rows, and replacement replay. Use distinct closed event types per variant because hub context is static.
- ACK timeout regressions (`EventQueueTests.AckTimeout.cs`) cover bounded open-stream reads, replay and backlog drain, event-expiry bounds, app/connection cancellation with observed read cleanup, timer validation, and committed-inbox deduplication. A real Kestrel HTTP/2 gRPC test verifies `DeadlineExceeded` and reconnection with two pending rows. It shares the test-local `LiveDeliveryAckHost<TEvent>` setup and asynchronous host/channel teardown with the unread-write regressions, retaining separate timeout, message-size, and lifetime inputs. Theory variants use distinct closed event types to isolate static hub state.
- Inbox-store retry tests (`EventQueueTests.StoreRetry.cs`) pair ordinary/ACK receivers with two failed writes followed by success. They assert callback counts `1, 2`, record identity, exception/token/event-type forwarding, store log context, throwing-callback isolation with the `store-event` label, persistence, signaling, and ACK acceptance. Both receive paths use `SubscriberContext.RetryStoreEvent` for reporting while retaining their protocol-specific storage operations.
- Receiver supervision tests (`EventQueueTests.ReceiverSupervision.cs`) pair ordinary/ACK paths for throwing receive callbacks, cancellation during reconnect delay, disposal before replacement, and error-count resets. They also protect protocol-specific call-creation exception boundaries and ACK count reset after successful writes, including duplicate acceptance following a failed ACK.
- Subscriber adapter startup coverage (`EventQueueTests.DeliveryAck.cs`, `subscriber_adapter_start_persists_and_executes_event`) starts ordinary and ACK adapters with active tokens and checks inbox persistence, handler completion, transport selection, and connection release. Separate closed event types isolate static adapter storage.
- Delivery-ack queue tests (`EventQueueTests.DeliveryAck.cs`) start receiver/executor workers directly with a 50 ms receiver retry delay and await them during session teardown. Subscriber registration/default-id tests retain the subscriber path. Failed and blocked ACK-write regressions observe an empty inbox fetch before publishing, use execution expiry below the 10-second fallback poll, and assert prompt execution through the production signal. Repeated dropped ACKs verify duplicate acknowledgement and one handler invocation. Ambiguous-commit coverage throws from `AfterStore` after persisting the inbox row, then verifies a duplicate retry wakes the idle executor within a three-second execution window. Active-handler duplicate coverage gates the handler through repeated replays and verifies one invocation; completed-key reconnect coverage verifies completed rows stay excluded. Lifecycle pruning tests publish a fresh event after reconnect and assert it is the only delivery, avoiding fixed observation sleeps. The focused registry metadata theory in `EventQueueTests.SubscriberLifecycle.cs` covers new/existing ordinary registration, exclusive acquisition/rejection, and restoration across configured and previously known states, asserting count policy, timestamp refresh, semaphore identity, and unchanged rejected snapshots.
- Job-queue idempotency, gRPC reflection, and AOT binding/jobs live under the matching `Tests/UnitTests`, `Tests/IntegrationTests/FastEndpoints/RPCTests`, and `Tests/NativeAotTests` folders. Do not stand up a second in-process event hub with default storage types (see [gotchas.md](gotchas.md)).
- Financial HTTP idempotency: real Kestrel response-lifecycle tests in `FinancialResponseCaptureTests.cs`; unit tests in `Tests/UnitTests/FastEndpoints/Financial*.cs` / `MemoryFinancialIdempotencyStoreTests.cs`; harness endpoints in `TestHarness/Web/[Features]/TestCases/FinancialIdempotency/`; integration in `Tests/IntegrationTests/FastEndpoints/FinancialIdempotencyTests/` (`Sut` for the memory path, `FinancialIdempotencyFaultSut` for in-flight/`Complete` failure fakes).
- Unit tests that host `WebApplication` + `UseFastEndpoints()` must isolate process statics ([gotchas.md](gotchas.md)). Do not add a second in-process host in `Unit.FastEndpoints` without that pattern.

## OpenAPI snapshots
- `EnumSchemaTransformerTests` hosts an isolated TestServer with the real ASP.NET schema pipeline and FE enum transformer. It covers mixed property converters, nullable values, naming policies, and unchanged shared enum components. `Int.OpenApi.csproj` enables `Microsoft.AspNetCore.OpenApi.Generated` interceptors for its `AddOpenApi` registration. Nullable enum request examples (mismatch, JSON null, and a matching value) are covered by `OperationTransformerEdgeCaseTests` against the `Swagger Review` document. `OperationSchemaHelpersTests` covers sample generation and invalid-example replacement with leading null enum entries, cloned values, and empty/all-null enum lists.
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
