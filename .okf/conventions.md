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
- Private fields: camelCase with `_` prefix (editorconfig / ReSharper rules).
- Parameters: camelCase; private constants: PascalCase.
- Packages: `FastEndpoints.<Area>` NuGet IDs matching project folders.

## Style
- C# latest language version; **nullable enable**; implicit usings.
- Indent 4 spaces; LF; UTF-8 (`.editorconfig`).
- Prefer existing partial-class splits for large types (`Endpoint.*.cs`).
- XML docs generated for packages (`GenerateDocumentationFile`); suppress noise via project `NoWarn`.
- Accessibility modifiers: editorconfig prefers not requiring explicit modifiers in some cases; follow neighboring code.
- `CS8618` suppressed for `*Request.cs`, `*Response.cs`, `*Model*.cs`, `*Endpoint.cs` patterns.

## Errors and validation
- Request validation: FluentValidation via `Validator<TRequest>` / endpoint `Validator` configuration.
- Validation failures flow through endpoint pipeline; error response shape controlled by `Config.Errors` (including ProblemDetails helpers).
- Prefer framework hooks (`OnBeforeValidate`, send helpers) over ad-hoc MVC filters.

## APIs and data
- REPR: Request DTO + Endpoint + optional Response DTO; configure routes/verbs/auth in `Configure()`.
- Mappers: `IMapper` / request-response mappers; **stateless** (singleton lifetime expectation).
- HTTP idempotency modes and registration: [architecture.md](architecture.md#communication). Settlement and persistence constraints: [financial-idempotency.md](financial-idempotency.md).

## Config and DI
- Global options: `UseFastEndpoints(c => { c.Serializer…; c.Endpoints…; })` → `FastEndpoints.Config` (`Cfg` alias).
- Endpoint ctor DI supported; property injection possible with attributes/options.
- Manual resolve façade: `Endpoint`/`Group`/`Mapper` inherit `ServiceResolverClient`; `Validator`/`BinderContext`/`HttpContext` extensions forward via `ServiceResolverClient.Forward` (do not re-copy the eight Resolve methods).
- Central package versions: [dependencies.md](dependencies.md). Do not hardcode versions in csproj.

## Legacy packages
- Keep modern OpenApi and legacy NSwag/Swagger work scoped to the requested stack. Harness selection: [testing.md](testing.md#harness-documentation-stack).

## Sources
- `.editorconfig`
- `Src/Library/Endpoint/`
- `Src/Library/Config/Config.cs`
- `TestHarness/Web/[Features]/`
