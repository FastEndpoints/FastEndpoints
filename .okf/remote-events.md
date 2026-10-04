---
type: Event
title: Remote Events
description: Remote event delivery protocols, storage contracts, worker ownership, and reliability constraints.
tags: [architecture, data, gotcha]
---

# Remote Events

Remote commands and wire-format selection: [architecture.md](architecture.md#communication). Accepted owner policies: [gotchas.md](gotchas.md#accepted-policies-and-review-exclusions). Regression locations: [testing.md](testing.md#domain-regression-map).

## Protocol and storage contracts
- Default delivery uses server-streaming `sub`. `IEventHubDeliveryAck<T>` switches every hub on that provider type to duplex `sub-ack`; subscribers opt in with `IEventSubscriberDeliveryAck<T>`. Upgrade both sides together. One provider type speaks one protocol.
- ACK confirms inbox persistence. Handler execution follows independently. Use durable, non-destructive hub storage for ACK delivery.
- Inbox keys are the hub `TrackingID`, protected by a unique index. `StoreEventAsync` throws `DuplicateEventDeliveryException` on conflict. Duplicate acceptance preserves the original expiry, retention, and completion state.
- Signal the executor after successful insertion or duplicate acceptance, before awaiting the ACK write. This also wakes a previously committed row whose storage response failed before signaling.
- Subscriber identity remains stable after opt-in: default ids retain the `EventSubscriber<...>` hash; explicit ids are unchanged.
- The ACK dispatcher holds exclusive subscriber ownership for the call. A second connection with the same id receives `FailedPrecondition`. Acquisition, timeout validation, and awaited cancellation cleanup precede release in `finally`.
- `DeliveryAckTimeout` defaults to 30 seconds. Write and ACK-read phases have separate budgets, each capped afresh by remaining replay lifetime. Timeout closes the call with `DeadlineExceeded`, leaves the durable row pending, and releases ownership after cancellation cleanup. Unacknowledged rows still expire.
- Empty ACK tracking ids are completed/skipped. Ordinary stream failures retain reconnect/requeue behavior.

## Retention and execution expiry
- ACK inbox records implement `IEventDeliveryAckStorageRecord` and persist nullable `RetainUntil`, calculated from hub `ReplayUntil` plus `DeliveryAckClockSkewAllowance` (default five minutes, non-negative).
- Apply the supplied purge predicate. Completed or execution-expired ACK rows retain their key through `RetainUntil`; null retention preserves ordinary-row cleanup in mixed storage.
- Upgrade hubs first and conservatively backfill old ACK retention before cleanup. Missing replay deadlines stop the receiver. The guarantee assumes bounded clock lead/in-flight delays and unchanged hub expiry after delivery.
- Execution expiry is refreshed immediately before each ACK insertion attempt. The accepted slow-successful-insert boundary is documented in [review exclusions](gotchas.md#accepted-policies-and-review-exclusions).

## Worker ownership
| Owner | Responsibility |
| --- | --- |
| `EventHub.OnDeliveryAck` | Read/validate hello, then delegate |
| `EventDeliveryAckDispatcher` | Exclusive connection, logging/rejection, phase deadlines, ACK sequencing |
| `HubContext` | Pending query/materialization, retrieval and completion retries, deserialization, idle waiting |
| `EventDispatcherWorker.RequeueBatchAsync` | Best-effort in-memory suffix recovery |
| `EventSubscriberRuntime<,,,>` | Purge setup, handler dependencies, shared receiver/executor semaphore, worker assembly |
| `EventReceiveSupervisor<TEvent>` | Reconnect delay, receive error counts, callback isolation, cancellation and terminal logs |
| `EventSubscriberRetentionPolicy<TStorageRecord>` | Capability/allowance validation, retained-key deadline, side-effect-free purge predicate |
| `EventSubscriberStorage<,>` | Provider setup and hourly purge worker lifetime |

- `GetNextNonEmptyBatch` returns a non-empty batch or null on linked cancellation, including disposed-signal shutdown. Retrieval error counts reset after a successful fetch. Dispatchers keep protocol-specific batch limits.
- Both dispatchers retry deserialization three times with one-second delays, including null payloads. Terminal recovery and completion follow the accepted poison-event policy. Cancellation leaves durable rows pending.
- Ordinary completion remains storage-mode-sensitive; ACK completion uses the shared durable retry path. In-memory requeue uses `CancellationToken.None` on deserialization cancellation and the linked connection/app token on stream failure.
- Receive sessions dispose calls before reconnect delay. Ordinary initial creation failures propagate, ordinary replacement creation failures terminate, and ACK creation failures retry.
- Retention validation runs at ACK adapter construction and allowance is read again at receiver startup. Adapter logger identities and storage caches remain protocol-specific.

## Test isolation
`EventHub<,,>` sets the static `EventHubStorage<TStorageRecord,TStorageProvider>.Provider`. A second host with default in-memory storage types can replace the main SUT provider. Use distinct closed event/storage types for independent hubs, adapters, and theory variants. Deadline tests use real Kestrel HTTP/2 where transport backpressure matters; helper tests use internal retry-delay overrides. Production retrieval and deserialization retries retain five-second and one-second delays respectively.

## Sources
- `Src/Messaging/Messaging.Remote/Server/Events/`
- `Src/Messaging/Messaging.Remote.Core/Client/Events/`
- `Src/Messaging/Messaging.Remote.Core/Common/EventDelivery.cs`
- `Tests/UnitTests/FastEndpoints/EventQueueTests.DeliveryAck.cs`
- `Tests/UnitTests/FastEndpoints/EventQueueTests.Deserialization.cs`
