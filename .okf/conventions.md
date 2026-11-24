---
type: Reference
title: Conventions
description: Coding, naming, and design conventions used across FastEndpoints packages.
tags: [conventions]
---

# Conventions

## Naming
- Public API namespace: mostly flat `FastEndpoints` (file-scoped / namespace-per-file patterns common).
- Endpoint types often named `Endpoint` inside a feature namespace (see harness), not `FooEndpoint`.
- Private fields: `_camelCase` (`.editorconfig` enforces it only for private static readonly; instance fields follow it by convention).
- Parameters: camelCase; private constants: PascalCase.

## Style
- C# latest language version; **nullable enable**; implicit usings.
- Indent 4 spaces; LF; UTF-8 (`.editorconfig`, which also holds the ReSharper `resharper_*` settings; `*.DotSettings.user` files are gitignored and user-local).
- Prefer existing partial-class splits for large types (`Endpoint.*.cs`).
- XML docs generated for packages (`GenerateDocumentationFile`); suppress noise via project `NoWarn`.
- `CS8618` suppressed for `*Request.cs`, `*Response.cs`, `*Model*.cs`, `*Endpoint.cs` patterns.
- Unwrap `Nullable<T>` with `type.GetUnderlyingType()` (`Src/Library/Extensions/ReflectionExtensions.cs`) instead of `Nullable.GetUnderlyingType(type) ?? type` where `FastEndpoints` internals are available, except in independently versioned `Src/Agents/` addons ([agents.md](agents.md)).

## Errors and validation
- Request validation: FluentValidation via `Validator<TRequest>`; error response shape is owned by `Config.Errors` (including ProblemDetails helpers).
- Prefer framework hooks (`OnBeforeValidate`, send helpers) over ad-hoc MVC filters.

## Config and DI
- Global options: `UseFastEndpoints(c => { c.Serializer…; c.Endpoints…; })` → `FastEndpoints.Config` (`Cfg` alias).
- Endpoint ctor DI supported; property injection possible with attributes/options.
- Manual resolve façade: `Endpoint`/`Group`/`Mapper` inherit `ServiceResolverClient`; `Validator`/`BinderContext`/`HttpContext` extensions forward via `ServiceResolverClient.Forward` (do not re-copy the eight Resolve methods).

## Dependencies and versioning
- Prefer project references and central package versions (`Directory.Packages.props`); do not invent reverse package dependencies. Details: [dependencies.md](dependencies.md#packages), [architecture.md](architecture.md#dependency-rules).
- Keep Agents addons independently versioned when touching `Src/Agents/`: [agents.md](agents.md), [workflows.md](workflows.md#pack-and-publish).

## Legacy packages
- Keep modern OpenApi and legacy NSwag/Swagger work scoped to the requested stack. Harness selection: [testing.md](testing.md#harness-documentation-stack).

## Sources
- `.editorconfig`
- `Src/Library/Endpoint/`
- `Src/Library/Config/Config.cs`
- `TestHarness/Web/[Features]/`
