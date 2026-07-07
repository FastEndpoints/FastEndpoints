---
type: Reference
title: Gotchas
description: Practical traps and non-obvious constraints for FastEndpoints work.
tags: [gotchas, constraints]
---

# Gotchas

- Main solutions are `.slnx`; if a tool cannot load `.slnx`, load the relevant `.csproj` directly.
- `Src/Agents/Directory.Build.props` must import `../Directory.Build.props`; without it, shared source properties disappear because MSBuild stops at the nearest props file.
- `Src/Generator/` intentionally targets `netstandard2.0`; do not inherit source multi-targeting there.
- Roslyn `Microsoft.CodeAnalysis.CSharp` is pinned at `[4.11.0]` for net8 compatibility.
- Kiota builder is pinned at `[1.29.0]` because newer Kiota expects Microsoft.OpenApi 3.x while ASP.NET OpenAPI still uses 2.x.
- NSwag projects/packages are legacy and marked for future deprecation; prefer `Src/OpenApi/` for new OpenAPI work unless the task targets legacy Swagger/client generation.
- `FastEndpoints.slnx` currently comments out Agents package projects and legacy Swagger test folders even though files exist.
- Integration test parallelization differs locally vs CI; Azure rewrites `Tests/IntegrationTests/FastEndpoints/xunit.runner.json` before testing.
- OpenAPI/Swagger AOT export targets intentionally run a JIT build before Native AOT publish.
- Generated output belongs in `obj/`, `Generated/FastEndpoints`, `wwwroot/openapi`, or ignored Native AOT harness paths; do not hand-edit generated output unless testing generation itself.
- Strong-name signing is enabled via props using repo key files; changing signing affects package identity/compatibility.
- The main package exposes broad public APIs; changing endpoint setup/binding/validation behavior usually needs both unit and integration tests.

## Sources

- `FastEndpoints.slnx`
- `Directory.Packages.props`
- `Src/Agents/Directory.Build.props`
- `Src/Generator/FastEndpoints.Generator.csproj`
- `Src/OpenApi/FastEndpoints.OpenApi.targets`
- `Src/Swagger/FastEndpoints.Swagger.targets`
- `azure-pipeline.yml`
- `.gitignore`
