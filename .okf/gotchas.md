---
type: Reference
title: Gotchas
description: Non-obvious traps for agents working in the FastEndpoints monorepo.
tags: [gotcha]
---

# Gotchas

- **AOT discovery:** reflection `AddFastEndpoints()` is not supported under AOT — use `AddFastEndpoints(DiscoveredTypes.All)` + Generator analyzer (`MainExtensions` / `EndpointData` warnings).
- **Generator package shape:** builds to `analyzers/dotnet/cs`; `DevelopmentDependency` is false for a reason (see Generator csproj / PR notes). Reference as analyzer, not normal library code.
- **AccessControl categories:** generator resolves string literals and compile-time string constants (`const`, `nameof`, etc.). Runtime/non-constant expressions for permission name or groups are ignored (no group membership).
- **Serializer contexts:** `GenerateSerializerContexts=true` pulls Generator.Cli (local dll in dev, `dotnet tool` when packaged). Dev path expects CLI built under `Generator.Cli/bin/.../net8.0/`.
- **Central versions pinned:** do not casually bump `Microsoft.CodeAnalysis.CSharp` (net8) or `Microsoft.OpenApi.Kiota.Builder` (OpenAPI 2 vs 3 clash) — comments in `Directory.Packages.props`.
- **Agents independent versioning:** `Src/Agents/Directory.Build.props` must `Import` parent props (MSBuild stops at first Directory.Build.props). Shared agent code is **linked compile**, not a NuGet.
- **Agents not in main slnx:** A2A/Mcp projects may be commented out of `FastEndpoints.slnx` — still real packages; check solution before assuming build inclusion.
- **Legacy vs modern OpenAPI:** harness prefers `FastEndpoints.OpenApi` + Scalar; NSwag Swagger/ClientGen live under Legacy and may have tests commented out.
- **CI filter:** tests with `Trait("ExcludeInCiCd","Yes")` never run in publish/Azure pipelines — don't rely on them as merge gates.
- **WAF cache:** one cached factory per `AppFixture` type; misuse of static state across tests can leak. Use fixture `ConfigureServices` for doubles.
- **Mappers are singletons:** no request state in mapper classes.
- **Signing / InternalsVisibleTo:** must use full public key from props; unsigned local hacks break friend assemblies.
- **User DotSettings:** `*.sln.DotSettings.user` is personal — don't treat as repo policy.
- **Do not commit secrets:** NuGet keys, JWT signing material for real envs.
- **Generated harness folders:** e.g. NativeAotChecker `Generated/`, `wwwroot/openapi/`, `aot/` are gitignored or build outputs — regenerate, don't hand-maintain.
- **Version citation:** always read `Src/Directory.Build.props` rather than OKF for current package version.
- **Docs are outside this repo:** user-facing docs are `../FE-Docs/src/content/docs/`. Library PRs that change public behavior without a FE-Docs update leave the site stale; OKF only points at docs, it does not replace them.

## Sources
- `Src/Library/Main/MainExtensions.cs`
- `Directory.Packages.props`
- `Src/Agents/Directory.Build.props`
- `Src/Generator/FastEndpoints.Generator.targets`
- `FastEndpoints.slnx`
