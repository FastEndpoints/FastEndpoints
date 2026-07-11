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

## MSBuild / CLI generation
- **`FastEndpoints.Generator.targets`:** when `GenerateSerializerContexts=true`, runs Generator.Cli to emit STJ serializer contexts into `SerializerContextOutputPath` (default `Generated/FastEndpoints`).
- **OpenApi targets:** export docs when properties like `ExportOpenApiDocs` set (see NativeAotChecker).
- Dev mode uses locally built `FastEndpoints.Generator.Cli.dll`; package mode installs local tool `FastEndpoints.Generator.Cli`.

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
