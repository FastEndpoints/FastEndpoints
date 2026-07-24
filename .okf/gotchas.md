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
- **Agents not in main slnx:** A2A/Mcp projects may be commented out of `FastEndpoints.slnx`; still real packages; check solution before assuming build inclusion.
- **Agents friend-assembly binary contract:** `Src/Library/Metadata.cs` grants `InternalsVisibleTo` to `FastEndpoints.Mcp` and `FastEndpoints.A2A`. Those packages are versioned independently of core, so recompiling add-ons against HEAD in this monorepo does **not** prove shipped NuGet binaries stay loadable. Changing an **internal** core member's name, arity, parameter types, or return type is a potential `MissingMethodException` / type-load break for already-published add-ons. Treat the stock below as a hard notice surface: if you touch any of these in `FastEndpoints` (Library), stop and check agent packages (rebuild + consider coordinated Agents release / version bump / dual overload when needed).
  - **Reflection / binding helpers** (`Src/Library/Binder/BinderExtensions.cs`, `Src/Library/Extensions/ReflectionExtensions.cs`):
    - `Type.BindableProps()` (return type is part of the CLR signature; e.g. `ICollection<PropertyInfo>` vs `PropertyInfo[]`)
    - `PropertyInfo.FieldName()`
    - `Type.IsComplexType()`
    - `Type.IsCollection()`
  - **Endpoint bootstrap / discovery** (`Src/Library/Main/EndpointBootstrap.cs`, `Src/Library/Main/EndpointData.cs`):
    - `EndpointBootstrap.CreateEndpoint(HttpContext, EndpointDefinition)`
    - `EndpointData.Found`
  - **EndpointDefinition internal fields** (`Src/Library/Endpoint/Auxiliary/EndpointDefinition.cs`):
    - `SerializerContext`
    - `Disposable`
    - `DisposableAsync`
  - **Config / type tokens** (`Src/Library/Config/Config.cs`, `Src/Library/Types.cs`):
    - `Config.SerOpts` (also via `using static FastEndpoints.Config` → `SerOpts.Options`)
    - `Types` class + fields used by agents: `EmptyRequest`, `FromBodyAttribute`, `ToHeaderAttribute`, `String`
  - **Primary agent call sites** (verify when restocking): `Src/Agents/Shared/AgentRequestBuilder.cs`, `AgentJsonPropertyNames.cs`, `AgentHttpContextFactory.cs`, `EndpointInvoker.cs`, `AgentEndpointCatalog.cs`; `Src/Agents/Mcp/McpToolSchemaFactory.cs`, `EndpointMcpToolSource.cs`; `Src/Agents/A2A/A2AJsonRpcEndpoint.cs`, `A2ASkillDispatcher.cs`, `Extensions.cs`.
  - **Restock rule:** when agents gain or drop an internal core call, update this bullet (and [monorepo-packages.md](monorepo-packages.md) agents section) in the same change.
- **Legacy vs modern OpenAPI:** harness prefers `FastEndpoints.OpenApi` + Scalar; NSwag Swagger/ClientGen live under Legacy and may have tests commented out.
- **OpenAPI form files:** `FastEndpoints.OpenApi` normalizes `IFormFile` and form-file collections to inline binary string schemas and removes generated `IFormFile*` components; preserve rewrite-before-removal ordering to avoid dangling refs.
- **OpenAPI schema `$ref` walking (Microsoft.OpenApi 2.x):** `OpenApiSchemaReference` often has null `Properties`/`Type` until resolved. Always `ResolveSchema()` before reading properties/composition; pass document `Components.Schemas` when `HostDocument` may be unset (e.g. `.http` export `SchemaPlaceholderBuilder`). Path-scoped cycle sets must key on the **resolved** schema so dual sibling refs to the same component both expand.
- **OpenAPI `.http` request bodies:** prefer media-type `Example` / first named `Examples` value, then schema/property `Example` → `Default`, else type placeholders (`""`/`0`/`false`). Media-type example is full replace (no merge). Form bodies still omitted; non-JSON uses `{{body}}`.
- **OpenAPI conditional validation:** FluentValidation presence rules (`NotNull`/`NotEmpty`) under rule- or component-level synchronous/asynchronous conditions must not emit unconditional `required`, non-null, or minimum-length/item constraints. Independent unconditional presence rules still apply.
- **CI filter:** tests with `Trait("ExcludeInCiCd","Yes")` never run in publish/Azure pipelines; don't rely on them as merge gates.
- **WAF cache:** one cached factory per `AppFixture` type; misuse of static state across tests can leak. Use fixture `ConfigureServices` for doubles.
- **Mappers are singletons:** no request state in mapper classes.
- **Signing / InternalsVisibleTo:** must use full public key from props; unsigned local hacks break friend assemblies. Agent packages rely on signed friend access (see **Agents friend-assembly binary contract** above).
- **User DotSettings:** `*.sln.DotSettings.user` is personal; don't treat as repo policy.
- **Do not commit secrets:** NuGet keys, JWT signing material for real envs.
- **Generated harness folders:** e.g. NativeAotChecker `Generated/`, `wwwroot/openapi/`, `aot/` are gitignored or build outputs; regenerate, don't hand-maintain.
- **Version citation:** always read `Src/Directory.Build.props` rather than OKF for current package version.
- **Docs are outside this repo:** user-facing docs are `../FE-Docs/src/content/docs/`. Library PRs that change public behavior without a FE-Docs update leave the site stale; OKF only points at docs, it does not replace them.
- **Event hub statics leak across tests:** `EventHub<,,>`'s ctor assigns `EventHubStorage<TStorageRecord,TStorageProvider>.Provider` (static and shared by every hub using the same storage types). Standing up a second handler server in-process with the default in-memory storage types clobbers the shared `Sut`'s provider and fails ~70 unrelated tests at random. Test the marshaller/binder directly, or use storage types unique to that test.
- **RPC wire format is per-connection and set-once:** `RemoteConnection.MarshallerFactory` must be assigned before any `Register<>()`, since each registration captures the format then (it throws otherwise). Server and client must match; both take the bound gRPC method name from the factory (MessagePack `""`, protobuf `Execute`). Its default is resolved from DI, so in a process that is *both* a handler server and a client, `AddHandlerServer(marshaller:)` also becomes the default for every outbound `RemoteConnection`; set it explicitly per connection when talking to a server on a different format.
- **Reflection needs protobuf:** `MapHandlerReflection()` throws at startup unless `AddHandlerServer(marshaller: new ProtobufMarshallerFactory())` is set (`AddHandlerReflection()` itself only registers services and does not validate). MessagePack has no descriptors to publish. Attribute-free field numbers are positional/alphabetical, so adding or renaming a property renumbers the rest; annotate `[ProtoContract]`/`[ProtoMember(n)]` to pin a contract that must survive changes.
- **Reflection describes less than it serializes:** the wire handles more shapes than the descriptor generator maps. `DateTime`/`DateOnly`/`TimeOnly`/`TimeSpan`/`decimal`/`Guid`/`Uri`, dictionary members and nested command types are skipped with a warning (`CommandNotDescribable`); those handlers still execute (via protobuf-net inbuilts), they're just not listed. Only field *numbering* is shared with the marshaller's model; the CLR→proto type mapping is separate. Message-valued maps (`Dictionary<K, Dto>`) are wire-supported: `Register` walks map key/value types before the `KeyValuePair<,>` serializability check (`IsMessage(KeyValuePair<,>)` stays false so descriptors never publish empty map entries).
- **Protobuf BCL denylist:** `ProtobufMarshallerFactory.IsMessage` / `IsNonMessageType` must keep known BCL specials out of attribute-free `Model.Add` (empty/hollow contracts → wire `0A00` / `default` or metadata-only corruption). Supported non-messages that still round-trip via protobuf-net inbuilts: primitives, enums, `string`, `byte[]`, `decimal`, `DateTime`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Uri`, and `System.Type` (assembly-qualified name; kept non-message via the `MemberInfo` hierarchy so the inbuilt serializer is used). Unsupported exact types (fail at `Create`/`Register`, never silent-drop): `DateTimeOffset`, `Half`, `Int128`, `UInt128`, `Version`, `object`, `JsonElement`, `JsonDocument`, `StringBuilder`, `TimeZoneInfo`, `IPAddress`, `Array`. Unsupported hierarchies (`IsAssignableFrom`, fail when `!CanSerialize`): `Exception`, `Stream`, `TextReader`, `TextWriter`, `EndPoint`, `Delegate`, `MemberInfo` (except `Type`, which `CanSerialize`), `Assembly`, `Module`. Open JSON should be `string`/`byte[]` until a real surrogate exists. Descriptor path uses the same rules so these are never published as empty nested messages. Empty user DTOs with zero r/w props remain valid messages.
- **Job queue idempotency:** `IdempotencyKeyFor<TCommand>(Func<TCommand,string?>)` requires storage record `IHasIdempotencyKey` (validated at `UseJobQueues`). Uniqueness lasts until row purge (completed rows still block). Providers must throw `DuplicateJobException` with existing `TrackingID` on unique violation; library does not catch raw DB unique errors. Null/empty/whitespace keys from the selector skip dedupe.
- **Handler short-circuit is opt-in:** pre-processors always skip the handler when `ResponseStarted` is true. `OnBeforeHandle*` does not, unless `DontExecuteHandlerIfResponseStarted()` is set (property `SkipHandlerIfResponseStarted`). Early return also skips `OnAfterHandle*`; post-processors still run.

## Sources
- `Src/Library/Metadata.cs`
- `Src/Library/Binder/BinderExtensions.cs`
- `Src/Library/Extensions/ReflectionExtensions.cs`
- `Src/Library/Main/EndpointBootstrap.cs`
- `Src/Agents/Shared/` · `Src/Agents/Mcp/` · `Src/Agents/A2A/`
- `Src/Agents/Directory.Build.props`
- `Directory.Packages.props`
- `FastEndpoints.slnx`
