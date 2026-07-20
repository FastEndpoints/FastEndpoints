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
- Pre/post processors: `IPreProcessor<TRequest>`, `IPostProcessor<TRequest,TResponse>`.
- Mappers: `IMapper` / request-response mappers; **stateless** (singleton lifetime expectation).
- Commands/events: implement `ICommand` / `IEvent` (+ handlers); job queue builds on commands.
- Optional attributes: `DontRegister`, `DontInject`, `HideFromDocs`, `RegisterService`, etc. in Attributes package.

## Config and DI
- Global options: `UseFastEndpoints(c => { c.Serializer…; c.Endpoints…; })` → `FastEndpoints.Config` (`Cfg` alias).
- Endpoint ctor DI supported; property injection possible with attributes/options.
- Service registration generator can emit registration from attributes when generator is referenced.
- Central package versions only in `Directory.Packages.props`; do not hardcode versions in csproj except intentional `VersionOverride` / constrained ranges already present.

## Testing conventions
- Integration: `AppFixture<TProgram>` / collection fixtures from `FastEndpoints.Testing`.
- Prefer typed HTTP helpers (`POSTAsync<TEndpoint, TRequest, TResponse>`) over raw URLs when endpoint types exist.
- Mark flaky/heavy tests with `[Trait("ExcludeInCiCd", "Yes")]` to match CI filter.

## YAGNI
- Keep changes minimal and consistent with surrounding package boundaries.
- Do not reintroduce NSwag/Swagger paths as default when OpenApi is the active harness path unless task is legacy package work.

## Sources
- `.editorconfig`
- `Src/Library/Endpoint/`
- `Src/Library/Config/Config.cs`
- `Src/Testing/`
- `TestHarness/Web/[Features]/`
