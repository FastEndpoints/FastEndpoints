---
type: Playbook
title: Workflows
description: Build, test, pack, generation, cleanup, and release workflows for FastEndpoints.
tags: [workflows, commands]
---

# Workflows

## Prerequisites

- Install .NET SDKs for target frameworks used by the task. GitHub release workflow installs 8.x, 9.x, and 10.x SDKs; Azure uses 10.x SDK with preview versions enabled.
- Use `FastEndpoints.slnx` for the main repo. Some tooling may not support `.slnx`; load a specific `.csproj` when a tool cannot load `.slnx`.

## Build

```bash
dotnet build FastEndpoints.slnx -c Release
```

Target a single package while iterating:

```bash
dotnet build Src/Library/FastEndpoints.csproj -c Debug
```

Native AOT harness solution:

```bash
dotnet build NativeAot.slnx -c Release
```

## Test

Run all main solution tests as release workflow does:

```bash
dotnet test FastEndpoints.slnx -c Release --verbosity minimal --filter "ExcludeInCiCd!=Yes"
```

Run all projects under `Tests/` as Azure pipeline does:

```bash
dotnet test Tests/**/*.csproj -c Release --filter ExcludeInCiCd!=Yes
```

Run a targeted test project:

```bash
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
```

## Pack and release

Release workflow on `v*` tags:

```bash
dotnet test FastEndpoints.slnx -c Release --verbosity minimal --filter "ExcludeInCiCd!=Yes"
dotnet pack FastEndpoints.slnx -c Release
```

NuGet publishing uses `NUGET_API_KEY` from GitHub secrets; never copy secret values into OKF or docs.

## Serializer context generation

The generator CLI can be run directly:

```bash
fastendpoints-generator MyProject.csproj --output Generated/FastEndpoints
```

MSBuild integration is opt-in in a consumer project:

```xml
<PropertyGroup>
    <GenerateSerializerContexts>true</GenerateSerializerContexts>
</PropertyGroup>
```

The generator target can build the local CLI in development mode or install/update the local tool when consumed from NuGet.

## OpenAPI/Swagger export before AOT publish

- New OpenAPI package uses `ExportOpenApiDocs=true` and `OpenApiExportPath` (default `wwwroot/openapi`).
- Legacy Swagger package uses `ExportSwaggerDocs=true` and `SwaggerExportPath` (default `wwwroot/openapi`).
- Both targets run a JIT build before Native AOT publish and copy exported docs into publish output when successful.

## Cleanup

```bash
./clean.sh
```

This removes `bin` and `obj` directories recursively.

## Sources

- `.github/workflows/publish-to-nuget.yml`
- `azure-pipeline.yml`
- `clean.sh`
- `Src/Generator.Cli/README.md`
- `Src/Generator/FastEndpoints.Generator.targets`
- `Src/OpenApi/FastEndpoints.OpenApi.targets`
- `Src/Swagger/FastEndpoints.Swagger.targets`
