---
type: Generated
title: Generated Code
description: Roslyn generators, MSBuild targets, and build outputs agents must not hand-edit.
tags: [layout]
---

# Generated Code

## Roslyn generators (`Src/Generator`)
The package ships under `analyzers/dotnet/cs`, with `DevelopmentDependency=false`. Consume it as an analyzer (see the project-reference example below).

| Generator | Purpose |
| --- | --- |
| `DiscoveredTypesGenerator` | Emits discovered endpoint/handler/validator/etc. type lists (`DiscoveredTypes`) for AOT-friendly `AddFastEndpoints` |
| `AccessControlGenerator` | Permission constants/groups from `AccessControl(...)` calls; category args resolve as string literals or compile-time string constants (`const`, `nameof`, etc.) via semantic model |
| `ReflectionGenerator` | Reflection cache support for reduced runtime reflection |
| `ServiceRegistrationGenerator` | DI registration from attributes |
| `GenericProcessorTypesGenerator` | Generic pre/post processor type materialization |

The discovery whitelist is owned by `DiscoveredTypesGenerator`; check it when adding a discoverable contract.

Open generic classes are skipped (`TypeParameterList` is not null). A non-generic class nested inside an open generic is also skipped (`IsNestedInGenericType()`): it is generic at runtime (`Type.IsGenericType`), `AssemblyScanner` drops it, and emitting `Outer<T>.Inner` does not compile.

Incremental pipeline rules (all generators):
- No generator instance state. The root namespace comes from `RootNamespaceProvider()` (or a `CompilationProvider.Select`) combined into the output step, never from a field set in a transform.
- Transform results must be value-equal and hold no `ISymbol`/`SyntaxNode`: use records of strings, with `EquatableArray<T>` for collections. A partial comparer (span length, display name only) leaves stale output in the IDE; command-line builds won't show it, so cover it with a reused-driver test.
- Emit type names via `ToFullName()` (`global::`-qualified) and property names via `EscapeKeyword()`, because generated files live in `namespace {AssemblyName}` and user namespaces/types can shadow unqualified names.
- Endpoint DTO types come from `GetEndpointBase()` (`FastEndpoints.Endpoint<TRequest, TResponse>`), not the immediate base type; `EndpointWithoutRequest*` resolves to `EmptyRequest`.

`ReflectionGenerator` derives registration namespaces from the assembly name, not endpoint namespaces. When `FastEndpoints.ReflectionCache` is available, it emits `ReflectionData.g.cs` even without non-generic classes. Messaging-only compilations without that HTTP contract emit no reflection registration. Each endpoint is walked with its own `TypeCollector`; `Generate` merges models by type name (first wins), so a DTO's model must not depend on which endpoint reached it. Property and constructor selection mirror the runtime binder (`BinderExtensions`): most-derived property per name, attributes inherited through `OverriddenProperty`, only `JsonIgnoreCondition.Always` excluded, fewest-parameter public constructor with optional and `params` arguments omitted (C# only allows `params` after an optional parameter, so the remaining arguments stay positional) (no public constructor means no `ObjectFactory`). `GenericProcessorTypesGenerator` closes post-processors only with each endpoint's own request/response pair, matching `EndpointDefinition`'s `MakeGenericType`, and skips DTOs that fail the processor's `class`/`struct`/`unmanaged`/`new()`/type constraints (checked by name; `new()` also rejects DTOs with `required` members unless the public parameterless constructor carries `[SetsRequiredMembers]`; constraint types that reference type parameters are not checked). Generated bodies `global::`-qualify framework names and keep `using` directives above the namespace, so `{root}.System`-style namespaces can't shadow them. Regressions: `Tests/UnitTests/FastEndpoints/*GeneratorTests.cs`; native parity via NativeAotChecker's `ReflectionParityEndpoint` (overrides, `[JsonIgnore]` conditions) and `RecordEventHandlerEndpoint` (record discovery).

## MSBuild / CLI generation
- **`FastEndpoints.Generator.targets`:** when `GenerateSerializerContexts=true` (optional `SerializerContextOutputPath`, default `Generated/FastEndpoints`, and `GeneratorCliVersion`), runs Generator.Cli to emit STJ serializer contexts.
- **CLI type index:** `SourceFileWalker` records class, struct, record, and enum declarations so generic type arguments (including enum dictionary keys) emit fully qualified `typeof(...)` in `[JsonSerializable]`. The same single parse pass records endpoint classes (`EndpointDeclaration`, file then document order); do not re-walk syntax trees for discovery. Cache schema `v4` invalidates older `.fastendpoints-generator-cache` files; bump it whenever resolution results change for unchanged inputs. The cache also stores the root namespace, so an assembly name change regenerates. Zero serializable types still writes both files (comment-only context, no-op `AddSerializerContextsFrom*()`): STJ rejects a context without `[JsonSerializable]`, and output from an earlier run must not survive.
- **CLI external fallback:** unresolved capitalized names lazily load referenced-project sources, then NuGet metadata (started in parallel on the first fallback). The cache's `*Inspected` flags make every later up-to-date check hash those inputs, so `IsExternalFallbackCandidate` excludes `GeneratorConfig.WellKnownExternalTypes` and skipped names. `GeneratorConfig.IsSkippedNamespace` / `IsSkippedTypeName` are the single owner of the `SkipNamespaces` / `SkipTypes` policy for both emission (`ShouldSkipType`) and fallback; do not re-implement the matching. `WellKnownExternalTypes` only applies to unqualified names; a qualified or alias-expanded name (`Contracts.Results`) still falls back. An unqualified same-named type from another project is taken for the BCL type (documented in the changelog), so only add unambiguous BCL/framework names. `BuiltInCollectionTypes` only blocks generic roots, since a non-generic `Queue` may be a user DTO. Generic lookups use `Name`arity` in every index (local, NuGet full and simple names); NuGet display names already carry arity, so strip it before re-appending. Type lookup, the fallback order (local, referenced projects, NuGet), loader laziness and the parallel prefetch live only in `AnalysisContext.ResolveType` (loaders are passed to its constructor; `Program` only supplies the `IsExternalFallbackCandidate` decision). `NuGetPackagesInspected` must mean "loader consulted", not "prefetch started"; the prefetch runs under `Diagnostics.Capture`, and its warnings are reported only when the loader is consulted. `Main` flushes diagnostics in a `finally`, so every exit path (including up-to-date) prints them. The cache hashes are owned by the `Lazy` inputs in `ExecuteGenerator`, not by analysis: `.Value.Hash` is read only when the matching `*Inspected` flag is true, or a skipped scan runs. `Program.Cache.cs` owns both sides of that rule (`IsUpToDate`, `CreateCacheState`), and the cache is written after both generated files.
- **CLI namespace contract:** the targets pass `--assembly-name $(AssemblyName)` (standalone fallback: project file name). The CLI namespace and `AddSerializerContextsFrom*` suffix must use the same rules as the Roslyn generators. Those rules live only in `Src/Generator/NamespaceExtensions.cs`, which the CLI compiles by link (`ToValidNameSpace()` keeps dots and prefixes `_` to empty, digit-leading or keyword segments; `ToValidIdentifier(string.Empty)` strips all non-alphanumerics for method suffixes). Keep that file self-contained (BCL plus `Microsoft.CodeAnalysis.CSharp`, netstandard2.0-compatible), and don't re-add naming helpers to the CLI. `<RootNamespace>` is ignored on both sides. Divergence makes one `using` insufficient and breaks AOT setup for dotted names.
- **OpenApi targets:** `ExportOpenApiArtifactsBeforeAotPublish` exports `.json`/`.http` when `ExportOpenApiDocs`/`ExportHttpFiles` set (aliases keep old target names). Single JIT intermediate dir + combined CLI flags when both enabled; app one-call export orchestrator (`ExportOpenApiArtifactsAndExitAsync`, or either legacy `Export*AndExitAsync` alias) reads those flags (see NativeAotChecker).
- Dev mode uses `Src/Generator.Cli/bin/$(Configuration)/net10.0/FastEndpoints.Generator.Cli.dll`; `BuildFastEndpointsGeneratorCli` automatically builds a missing DLL before `CoreCompile` when serializer generation is enabled. Package mode installs/updates the local tool `FastEndpoints.Generator.Cli`, pinned to the package folder name (`.../<version>/build/..`; the CLI shares the library `<Version>`). An empty `GeneratorCliVersion` means a tool install and `--prerelease` update on every build, so keep that extraction working. Update passes `--allow-downgrade` when pinned, because a manifest from older targets may hold a newer prerelease; without it install and update both fail on every build. The `obj/` sentinel (`fastendpoints-tool.version`) is written only when install or update succeeds; an unconditional write would make a failed first install permanent, since later builds skip the install and `dotnet clean` doesn't remove it.
- NativeAotChecker isolates serializer contexts in `obj/$(Configuration)/$(TargetFramework)/SerializerCtx` and includes them after generation, including on a clean build. Do not include its old `Generated/` sources. Test fixture native binaries are isolated under each test TFM's output directory.

## Do not hand-edit
- Compiler-generated outputs under consumer `Generated/` folders
- NativeAotChecker `Generated/`, `wwwroot/openapi/`, publish `aot/` (gitignored)
- Packaged analyzer binary contents

Prefer changing **generator sources** under `Src/Generator/*.cs` or **targets**, then rebuild.

## Consuming in-repo
Harness example:
```xml
<ProjectReference Include="..\..\Src\Generator\FastEndpoints.Generator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false"/>
```
And `AddFastEndpoints(DiscoveredTypes.All)` in `Program.cs`.

## Sources
- `Src/Generator/DiscoveredTypesGenerator.cs`
- `Src/Generator/FastEndpoints.Generator.csproj`
- `Src/Generator/FastEndpoints.Generator.targets`
- `TestHarness/Web/Web.csproj`
- `TestHarness/NativeAotChecker/NativeAotChecker.csproj`
