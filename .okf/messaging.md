---
type: Reference
title: Messaging and Jobs
description: RPC wire compatibility, command dispatch lifetimes, job deduplication, and Native AOT constraints.
tags: [gotcha, architecture]
---

# Messaging and Jobs

Remote event delivery and storage contracts: [remote-events.md](remote-events.md).

## Remote RPC and reflection

- **RPC wire format is per-connection and set-once:** set `RemoteConnection.MarshallerFactory` before any `Register<>()` (registration captures it and throws otherwise). Server and client must match. Both take the bound gRPC method name from the factory (MessagePack `""`, protobuf `Execute`). The default comes from DI, so `AddHandlerServer(marshaller:)` also becomes the default outbound factory in a process that is both server and client. Set it per connection when the peer uses a different format.
- **Reflection needs protobuf:** `MapHandlerReflection()` throws at startup unless `AddHandlerServer(marshaller: new ProtobufMarshallerFactory())` is set. `AddHandlerReflection()` only registers services. MessagePack has no descriptors. Attribute-free field numbers are positional, so adding or renaming a property renumbers the rest. Pin with `[ProtoContract]` / `[ProtoMember(n)]` when the contract must survive edits.
- **Reflection describes less than it serializes:** the descriptor generator skips `DateTime`, `DateOnly`, `TimeOnly`, `TimeSpan`, `decimal`, `Guid`, `Uri`, dictionaries, and nested commands (`CommandNotDescribable`). Those handlers still run via protobuf-net. Only field numbering is shared with the marshaller. `IsMessage(KeyValuePair<,>)` stays false so map entries are not published as empty messages.
- **Protobuf BCL denylist:** `ProtobufMarshallerFactory.IsMessage` / `IsNonMessageType` keep BCL specials out of attribute-free `Model.Add` (hollow contracts corrupt the wire). Edit that denylist together with the descriptor tests. Do not copy the type list into OKF.

## Messaging and jobs

- **Job queue idempotency:** `IdempotencyKeyFor<TCommand>` requires storage record `IHasIdempotencyKey` (checked at `UseJobQueues`). Uniqueness is `(QueueID, IdempotencyKey)` until the row is purged, including completed rows. Providers throw `DuplicateJobException` with the existing `TrackingID`. The library does not catch raw DB unique violations. Null, empty, or whitespace selector results skip dedupe.
- **Event-bus hot path:** keep `_handlers` as `IEventHandler<TEvent>[]` for indexed/single-handler dispatch. Keep `Offload`, `OffloadOne`, `OffloadAll`, and `Filter` outside `Execute` to avoid per-publish display-class allocation. Single-handler `WaitForAny` uses `Task.WhenAny` so faults retain the mode’s uncaptured behavior.
- **Void command result:** keep public `Void` a zero-field struct with `Instance=default` so synchronous sends reuse the runtime’s cached `Task<Void>`. A class or extra field adds allocations. Audit `where TResult : class` on command/stream interfaces before changing it. Source: `Src/Messaging/Messaging.Core/Interfaces/ICommand.cs`; Library `Types.Void` means `typeof(void)`.
- **Job-queue AOT construction:** `UseJobQueues` uses `ActivatorUtilities.CreateInstance` and stores instances on `JobQueueBase.JobQueues`. Preserve that path; open-generic DI cannot close over value-type `Void` under Native AOT. Void jobs use class `JobQueueVoidResult`, with `ExecuteCommand` handling `ICommand` first. Keep that sentinel internal to queue construction: result-record/provider callbacks and failure-recovery lookup use public `Void`; result-type validation maps the sentinel back to `Void`. Source-generated result contexts need metadata for `Void`. `IJobTracker<>` remains open-generic because commands are classes.
- **Void command handler compatibility:** production no-result handlers may implement `ICommandHandler<TCommand>` or `ICommandHandler<TCommand, Void>`. Dispatch through the common two-argument interface; casting to the one-argument interface rejects valid discovered handlers. `CommandBusTests.Void_Command_Handler_Forms_Work_Through_Discovery` covers both forms with middleware and test-handler substitution.
- **Command middleware scope discovery:** discover middleware through the active request scope, even though executor singleton construction stays rooted. Cache only types; create instances per dispatch in the current scope. Constructor-injecting the enumerable resolves scoped middleware from the root and fails scope validation. Regression: `CommandBusScopedMiddlewareTests`.
- **Void command AOT execution:** `PrepareExecution` selects `VoidCommandHandlerExecutor<TCommand>` instead of closing `CommandHandlerExecutor<TCommand, Void>`. On dynamic-code platforms, resolve `IEnumerable<ICommandMiddleware<TCommand, Void>>` from DI so direct and helper registrations keep DI ordering. Under Native AOT, resolve middleware through `CommandMiddlewareRegistrations` and register closed `Register<TCommand, Void, TMiddleware>()`. Open registration throws for void commands without dynamic code. Propagate `NotSupportedException` rather than silently dropping the pipeline.
- **Lazy handler reflection:** keep `MakeGenericType` inside `CommandExtensions.HandlerInterfaceType`, invoked by `PrepareExecution` for first generic dispatch or `TestCommandHandlerMarker`. Hoisting it into every `ExecuteAsync` adds work and breaks AOT paths. Void test handlers use `ICommandHandler<TCommand>`; preserve `tHandlerOf1 is not null && tRes == Types.VoidResult`.

## Sources
- `Src/Messaging/`
- `Src/JobQueues/`
- `Tests/UnitTests/FastEndpoints/CommandBusTests.cs`
- `Tests/IntegrationTests/FastEndpoints/RPCTests/`
