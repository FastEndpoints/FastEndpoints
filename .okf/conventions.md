---
type: Reference
title: Conventions
description: Coding, endpoint, package, and validation conventions used in the repository.
tags: [conventions, style]
---

# Conventions

## C# and formatting

- Use UTF-8, LF line endings, spaces, and 4-space indentation from `.editorconfig`.
- C# uses latest language version, nullable enabled, and implicit usings for source/test projects via props files.
- Prefer `var` where apparent/built-in per `.editorconfig` suggestions.
- Accessibility modifiers are not required by style (`dotnet_style_require_accessibility_modifiers = never:error`).
- Many ReSharper formatter settings are encoded in `.editorconfig`; preserve existing formatting in touched files.

## Naming and layout

- Private field naming is underscore camelCase where configured; private constants are PascalCase.
- Feature/harness endpoint folders commonly use `Endpoint`, `Request`, `Response`, `Models`, and `Validator` classes under feature namespaces, e.g. `Uploads.Image.Save`.
- Keep feature-specific request/response/validator/model code near the endpoint when following test harness patterns.
- Keep package code inside its owning `Src/<Package>/` directory; avoid cross-package source sharing that bypasses `.csproj` references.

## Endpoint design

- Endpoint classes derive from FastEndpoints base classes such as `Endpoint<TRequest>`, `Endpoint<TRequest,TResponse>`, or no-request variants.
- Endpoint setup is expressed through the DSL in `Configure()`/endpoint definition: verbs, routes, auth, claims/permissions/scopes, validators, processors, summary/metadata, versioning, throttling, serializer contexts, and response behavior.
- Runtime applications register with `AddFastEndpoints(...)` and map/use with `UseFastEndpoints(...)` or `MapFastEndpoints(...)`.
- Maintain REPR intent: request DTO, endpoint behavior, and response DTO remain explicit and easy to locate.

## Validation and error behavior

- FluentValidation is a primary dependency of the main library.
- Validators usually derive from `Validator<TRequest>`.
- Data annotations are optional runtime configuration, not the default assumption for all endpoints.
- Preserve existing validation-failure behavior and tests when touching binding, validators, processors, or response serialization.

## Package and API discipline

- Centralize NuGet version changes in `Directory.Packages.props`.
- Package metadata and target frameworks mostly come from `Src/Directory.Build.props`; use project-level overrides only when required.
- Public package APIs need tests and compatibility awareness across `net8.0`, `net9.0`, and `net10.0`.
- Do not update Roslyn or Kiota dependencies without checking comments in `Directory.Packages.props`.

## Simplicity

- Prefer focused changes that follow nearby patterns.
- Avoid broad refactors across packages unless the task requires coordinated API/behavior changes.
- Do not edit generated files or build outputs manually.

## Sources

- `.editorconfig`
- `Src/Directory.Build.props`
- `Tests/Directory.Build.props`
- `TestHarness/Web/[Features]/Uploads/Image/Save/Endpoint.cs`
- `TestHarness/Web/[Features]/Uploads/Image/Save/Models.cs`
- `Src/Library/Endpoint/Endpoint.Setup.cs`
