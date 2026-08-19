---
type: Reference
title: Gotchas
description: Non-obvious traps for agents working in the FastEndpoints monorepo.
tags: [gotcha]
---

# Gotchas

- **AOT discovery:** reflection `AddFastEndpoints()` is not supported under AOT; use `AddFastEndpoints(DiscoveredTypes.All)` + Generator analyzer (`MainExtensions` / `EndpointData` warnings).
- **Generator package shape:** builds to `analyzers/dotnet/cs`; `DevelopmentDependency` is false for a reason (see Generator csproj / PR notes). Reference as analyzer, not normal library code.
- **AccessControl categories:** generator resolves string literals and compile-time string constants (`const`, `nameof`, etc.). Runtime/non-constant expressions for permission name or groups are ignored (no group membership).
- **Serializer contexts:** `GenerateSerializerContexts=true` pulls Generator.Cli (local dll in dev, `dotnet tool` when packaged). Dev path expects CLI built under `Generator.Cli/bin/.../net8.0/`.
- **Central versions pinned:** do not casually bump `Microsoft.CodeAnalysis.CSharp` (net8) or `Microsoft.OpenApi.Kiota.Builder` (OpenAPI 2 vs 3 clash); comments in `Directory.Packages.props`.
- **Agents independent versioning:** `Src/Agents/Directory.Build.props` must `Import` parent props (MSBuild stops at first Directory.Build.props). Shared agent code is **linked compile**, not a NuGet.
- **Agents friend-assembly binary contract:** `Src/Library/Metadata.cs` grants `InternalsVisibleTo` to `FastEndpoints.Mcp` and `FastEndpoints.A2A`. Those packages are versioned independently of core, so recompiling add-ons against HEAD in this monorepo does **not** prove shipped NuGet binaries stay loadable. Changing an **internal** core member's name, arity, parameter types, or return type is a potential `MissingMethodException` / type-load break for already-published add-ons. Treat the stock below as a hard notice surface: if you touch any of these in `FastEndpoints` (Library), stop and check agent packages (rebuild + consider coordinated Agents release / version bump / dual overload when needed).
  - **Reflection / binding helpers** (`Src/Library/Binder/BinderExtensions.cs`, `Src/Library/Extensions/ReflectionExtensions.cs`):
    - `Type.BindableProps()` (return type is part of the CLR signature; e.g. `ICollection<PropertyInfo>` vs `PropertyInfo[]`)
    - `PropertyInfo.FieldName()`
    - `Type.IsComplexType()`
    - `Type.IsCollection()`
  - **Endpoint bootstrap / discovery** (`Src/Library/Main/EndpointBootstrap.cs`, `Src/Library/Main/EndpointData.cs`, `Src/Library/Main/MainExtensions.cs`):
    - `EndpointBootstrap.CreateEndpoint(HttpContext, EndpointDefinition)`
    - `EndpointData.Found`
    - `MainExtensions.BuildRoute(StringBuilder, int, string, string?)` (extension; OpenApi + Agents route finalization)
  - **EndpointDefinition internal fields** (`Src/Library/Endpoint/Auxiliary/EndpointDefinition.cs`):
    - `SerializerContext`
    - `Disposable`
    - `DisposableAsync`
  - **Config / type tokens** (`Src/Library/Config/Config.cs`, `Src/Library/Types.cs`):
    - `Config.SerOpts` (also via `using static FastEndpoints.Config` → `SerOpts.Options`)
    - `Types` class + fields used by agents: `EmptyRequest`, `FromBodyAttribute`, `ToHeaderAttribute`, `String`
  - **Primary agent call sites** (verify when restocking): `Src/Agents/Shared/AgentRequestBuilder.cs`, `AgentJsonPropertyNames.cs`, `AgentHttpContextFactory.cs`, `EndpointInvoker.cs`, `AgentEndpointCatalog.cs`; `Src/Agents/Mcp/McpToolSchemaFactory.cs`, `EndpointMcpToolSource.cs`; `Src/Agents/A2A/A2AJsonRpcEndpoint.cs`, `A2ASkillDispatcher.cs`, `Extensions.cs`.
  - **Restock rule:** when agents gain or drop an internal core call, update this stock in the same change.
- **Legacy vs modern OpenAPI:** harness prefers `FastEndpoints.OpenApi` + Scalar; NSwag Swagger/ClientGen live under Legacy and may have tests commented out.
- **OpenAPI form files:** `FastEndpoints.OpenApi` normalizes `IFormFile` and form-file collections to inline binary string schemas and removes generated `IFormFile*` components; preserve rewrite-before-removal ordering to avoid dangling refs.
- **OpenAPI schema `$ref` (Microsoft.OpenApi 2.x):** `OpenApiSchemaReference` often has null `Properties`/`Type` until `ResolveSchema()`; pass `Components.Schemas` when `HostDocument` may be unset. Cycle sets must key on the **resolved** schema. See `Src/OpenApi/` walkers and `.http` `SchemaPlaceholderBuilder`.
- **OpenAPI `.http` bodies:** media-type `Example` / first named `Examples`, then schema `Example` → `Default`, else placeholders. Media-type example replaces wholly (no merge). Form omitted; non-JSON uses `{{body}}`.
- **OpenAPI conditional validation:** FluentValidation `NotNull`/`NotEmpty` under conditions must not emit unconditional `required` / non-null / min-length. Unconditional presence rules still apply.
- **CI filter:** tests with `Trait("ExcludeInCiCd","Yes")` never run in publish/Azure pipelines; don't rely on them as merge gates. To skip a whole project (e.g. `Int.OpenApi.Kiota`), set `IsTestingPlatformApplication=false` (and `IsTestProject=false`) under `CI`/`TF_BUILD`. MTP ignores `IsTestProject`; exit 8 is "zero tests ran", not a failed assertion.
- **NuGet push must `--skip-duplicate`:** Agents (`Mcp`/`A2A`) are independently versioned but packed from `FastEndpoints.slnx`. A core-only tag re-pushes the last Agents version; nuget.org 409s and `dotnet nuget push` stops, leaving later glob matches unpublished. Do not bump Agents versions just to make a core release succeed.
- **WAF cache:** one cached factory per `AppFixture` type; misuse of static state across tests can leak. Use fixture `ConfigureServices` for doubles.
- **Mappers are singletons:** no request state in mapper classes.
- **Signing / InternalsVisibleTo:** must use full public key from props; unsigned local hacks break friend assemblies. Agent packages rely on signed friend access (see **Agents friend-assembly binary contract** above).
- **User DotSettings:** `*.sln.DotSettings.user` is personal; don't treat as repo policy.
- **Do not commit secrets:** NuGet keys, JWT signing material for real envs.
- **Generated harness folders:** e.g. NativeAotChecker `Generated/`, `wwwroot/openapi/`, `aot/` are gitignored or build outputs; regenerate, don't hand-maintain.
- **Version citation:** always read `Src/Directory.Build.props` rather than OKF for current package version.
- **Docs are outside this repo:** `../FE-Docs/src/content/docs/` ([workflows.md](workflows.md)). Public behavior changes need a docs update there.
- **Event hub statics leak across tests:** `EventHub<,,>`'s ctor assigns `EventHubStorage<TStorageRecord,TStorageProvider>.Provider` (static and shared by every hub using the same storage types). Standing up a second handler server in-process with the default in-memory storage types clobbers the shared `Sut`'s provider and fails ~70 unrelated tests at random. Test the marshaller/binder directly, or use storage types unique to that test.
- **RPC wire format is per-connection and set-once:** `RemoteConnection.MarshallerFactory` must be assigned before any `Register<>()`, since each registration captures the format then (it throws otherwise). Server and client must match; both take the bound gRPC method name from the factory (MessagePack `""`, protobuf `Execute`). Its default is resolved from DI, so in a process that is *both* a handler server and a client, `AddHandlerServer(marshaller:)` also becomes the default for every outbound `RemoteConnection`; set it explicitly per connection when talking to a server on a different format.
- **Reflection needs protobuf:** `MapHandlerReflection()` throws at startup unless `AddHandlerServer(marshaller: new ProtobufMarshallerFactory())` is set (`AddHandlerReflection()` itself only registers services and does not validate). MessagePack has no descriptors to publish. Attribute-free field numbers are positional/alphabetical, so adding or renaming a property renumbers the rest; annotate `[ProtoContract]`/`[ProtoMember(n)]` to pin a contract that must survive changes.
- **Reflection describes less than it serializes:** descriptor generator skips `DateTime`/`DateOnly`/`TimeOnly`/`TimeSpan`/`decimal`/`Guid`/`Uri`, dictionaries, and nested commands (`CommandNotDescribable`); those handlers still run via protobuf-net inbuilts. Only field numbering is shared with the marshaller. `IsMessage(KeyValuePair<,>)` stays false so map entries are not published as empty messages.
- **Protobuf BCL denylist:** `ProtobufMarshallerFactory.IsMessage` / `IsNonMessageType` keep BCL specials out of attribute-free `Model.Add` (hollow contracts corrupt the wire). Edit that denylist and the descriptor tests together; do not copy the type lists here.
- **Job queue idempotency:** `IdempotencyKeyFor<TCommand>(Func<TCommand,string?>)` requires storage record `IHasIdempotencyKey` (validated at `UseJobQueues`). Uniqueness lasts until row purge (completed rows still block). Providers must throw `DuplicateJobException` with existing `TrackingID` on unique violation; library does not catch raw DB unique errors. Null/empty/whitespace keys from the selector skip dedupe.
- **Don't turn `HandleValidationFailure` into a local function:** in `Src/Library/Endpoint/Endpoint.cs` it is a private method taking `req`/`ranPreProcessors`/`ct` as parameters, called from the two failure paths in `ExecAsync`. Moving it back inside `ExecAsync` as an **async** local function that captures those locals makes Roslyn emit a heap `<>c__DisplayClass` allocated on *every* request, successful ones included, even though the helper only runs when binding or validation fails (verified: 48 bytes per request on 64 bit, and no closure type left in the compiled `Endpoint<TRequest, TResponse>` metadata). It cannot be named `ValidationFailed`, since that is already a public property of the same partial class (`Endpoint.Validation.cs`).
- **`EventBus<TEvent>._handlers` must stay a concrete `IEventHandler<TEvent>[]`:** the `//ToArray() is essential here!!!` in the ctor explains why the *store* is materialized (the `Select(CreateSingleton)` must not re-resolve handlers per publish). The *declared* type must not be widened back to `IEnumerable<>` either: `Execute` tests `handlers.Length == 0`, indexes into the array, and takes a zero-allocation single-handler fast path. Widening it reintroduces an enumerator per publish on the hottest messaging path. For the same reason `Execute` must keep the `Task.Run` lambdas of the `WaitForNone`/`WaitForAny` branches in the separate `Offload`/`OffloadOne`/`OffloadAll` methods (and keep `Filter` out of line): inlining them makes Roslyn hoist the `<>c__DisplayClass` to the top of `Execute`, allocating 40 bytes on every publish including the single-handler `WaitForAll` path, which is otherwise zero allocation (measured). Single-handler `WaitForAny` must still go through `Task.WhenAny` (not the raw `Task.Run` task) so handler faults stay non-capturable per `Mode` docs.
- **`EndpointDefinition.ResponseCacheControl` assumes startup-fixed response cache settings:** `ResponseCacheExecutor` builds the `Cache-Control` value once per endpoint and reuses it, which is only correct because `ResponseCache(...)` is `ThrowIfLocked()`-guarded, so `Location` and `Duration` cannot change after startup. If a runtime mutation path is ever added (per-request duration, hot-reloadable cache settings, or user code mutating the `ResponseCacheAttribute` it can reach through endpoint metadata), the cached string goes stale and must be invalidated with it. Covered by `Tests/UnitTests/FastEndpoints/ResponseCacheExecutorTests.cs`.
- **`EndpointRouteMapper` per-definition cleanup must stay outside the route loop:** `def.AttribsToForward = null` and `def.IsLocked = true` sit after the `foreach (var route in def.Routes)` block, not inside it. They were inside it until this was fixed, so a multi-route endpoint configured by attributes forwarded its unrecognized class attributes (`[EnableCors]`, `[EnableRateLimiting]`, user metadata attributes, ...) to the metadata of route 1 only, and the definition was locked while routes 2..N were still being mapped. The indentation is misleading (both lines are already dedented relative to the verb loop), so re-check the brace level rather than the indentation if you touch this. Covered by `Tests/UnitTests/FastEndpoints/EndpointRouteMapperTests.cs`. `AttribsToForward` is only populated on the attribute-configuration path (`EndpointExtensions.Initialize`'s `else if`), so `Configure()`-based endpoints never showed the bug.
- **Handler short-circuit is opt-in:** pre-processors always skip the handler when `ResponseStarted` is true. `OnBeforeHandle*` does not, unless `DontExecuteHandlerIfResponseStarted()` is set (property `SkipHandlerIfResponseStarted`). Early return also skips `OnAfterHandle*`; post-processors still run.

## Sources
- `Src/Library/Metadata.cs`
- `Src/Library/Binder/BinderExtensions.cs`
- `Src/Library/Extensions/ReflectionExtensions.cs`
- `Src/Library/Main/EndpointBootstrap.cs`
- `Src/Agents/Shared/` · `Src/Agents/Mcp/` · `Src/Agents/A2A/`
- `Src/Agents/Directory.Build.props`
- `Directory.Packages.props`
