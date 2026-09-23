---
type: Generated
title: Generated Code
description: Roslyn generators, MSBuild targets, and build outputs agents must not hand-edit.
tags: [layout]
---

# Generated Code

## Roslyn generators (`Src/Generator`)
| Generator | Purpose |
| --- | --- |
| `DiscoveredTypesGenerator` | Emits discovered endpoint/handler/validator/etc. type lists (`DiscoveredTypes`) for AOT-friendly `AddFastEndpoints` |
| `AccessControlGenerator` | Permission constants/groups from `AccessControl(...)` calls; category args resolve as string literals or compile-time string constants (`const`, `nameof`, etc.) via semantic model |
| `ReflectionGenerator` | Reflection cache support for reduced runtime reflection |
| `ServiceRegistrationGenerator` | DI registration from attributes |
| `GenericProcessorTypesGenerator` | Generic pre/post processor type materialization |

Whitelist for discovery includes `IEndpoint`, `IEventHandler`, `ICommandHandler`, stream command handlers, summaries, job storage providers, processors, command middleware, mappers, FluentValidation validators (`DiscoveredTypesGenerator`).

Open generic classes are skipped (`TypeParameterList` is not null). A non-generic class nested inside an open generic is also skipped: it is generic at runtime (`Type.IsGenericType`), `AssemblyScanner` drops it, and emitting `Outer<T>.Inner` does not compile in `DiscoveredTypes`.

## MSBuild / CLI generation
- **`FastEndpoints.Generator.targets`:** when `GenerateSerializerContexts=true` (optional `SerializerContextOutputPath`, default `Generated/FastEndpoints`, and `GeneratorCliVersion`), runs Generator.Cli to emit STJ serializer contexts.
- **CLI type index:** `SourceFileWalker` records class, struct, record, and enum declarations so generic type arguments (including enum dictionary keys) emit fully qualified `typeof(...)` in `[JsonSerializable]`. Cache schema `v2` invalidates older `.fastendpoints-generator-cache` files.
- **OpenApi targets:** `ExportOpenApiArtifactsBeforeAotPublish` exports `.json`/`.http` when `ExportOpenApiDocs`/`ExportHttpFiles` set (aliases keep old target names). Single JIT intermediate dir + combined CLI flags when both enabled; app one-call export orchestrator (`ExportOpenApiArtifactsAndExitAsync`, or either legacy `Export*AndExitAsync` alias) reads those flags (see NativeAotChecker).
- Dev mode uses the locally built `FastEndpoints.Generator.Cli.dll` under `Src/Generator.Cli/bin/.../net8.0/`. Package mode installs local tool `FastEndpoints.Generator.Cli`.

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
