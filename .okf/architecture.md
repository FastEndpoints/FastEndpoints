---
type: Architecture
title: Architecture
description: High-level module boundaries, runtime model, dependency directions, and invariants.
tags: [architecture, boundaries]
---

# Architecture

## Style

FastEndpoints is a multi-package .NET library repo. The main library exposes ASP.NET Core registration/middleware extensions and endpoint base classes. Applications define feature-oriented endpoint classes; FastEndpoints discovers endpoint types, builds endpoint definitions, registers routes, binds/validates requests, and executes endpoint handlers.

## Major components

| Area | Location | Role |
| --- | --- | --- |
| Main framework | `Src/Library/` | Endpoint base types, setup DSL, binding, validation, auth/security metadata, middleware, response helpers, testing hooks, X402 support. |
| Core utilities | `Src/Core/` | Shared service resolution and assembly scanning functionality used by other packages. |
| Attributes | `Src/Attributes/` | Attribute package shared with generators/consumers. |
| Messaging | `Src/Messaging/` | Messaging interfaces, command/event bus, remote messaging, and test helpers. |
| Job queues | `Src/JobQueues/` | Background command queueing and scheduling. |
| Command rules | `Src/CommandRules/` | Composable command rule dispatching over messaging/job queues. |
| OpenAPI | `Src/OpenApi/`, `Src/OpenApi.Kiota/` | Microsoft.AspNetCore.OpenApi document generation and Kiota client support. |
| Legacy OpenAPI/clients | `Src/Swagger/`, `Src/ClientGen/`, `Src/ClientGen.Kiota/` | NSwag-based Swagger and client generation; solution marks several as legacy. |
| Integrations | `Src/AspVersioning/`, `Src/OData/`, `Src/Security/`, `Src/HealthChecks/`, `Src/Testing/`, `Src/Agents/` | Optional packages layered on the main framework. |
| Generators | `Src/Generator/`, `Src/Generator.Cli/` | Roslyn source generator and CLI/MSBuild targets for serializer context generation. |

## Dependency rules

- Keep package dependencies layered through project references declared in each `.csproj`; do not introduce cycles.
- `Src/Library/FastEndpoints.csproj` depends on `Attributes`, `JobQueues`, and `Messaging`, and carries the main ASP.NET framework reference plus FluentValidation.
- Optional packages should depend on `Src/Library/` only when they extend endpoint/framework behavior.
- `Src/Generator/` targets `netstandard2.0` and references `Attributes`; preserve generator compatibility and analyzer packaging rules.
- `Src/Agents/Directory.Build.props` explicitly imports `Src/Directory.Build.props`; keep that import because MSBuild stops at the nearest props file.

## Runtime model

- Apps typically call `AddFastEndpoints(...)` during service registration and `UseFastEndpoints(...)`/`MapFastEndpoints(...)` during pipeline setup.
- Endpoint classes configure route verbs, routes, auth/permissions/claims/scopes, validation, processors, serializer contexts, response metadata, versioning, throttling, and other metadata through the endpoint setup DSL.
- Request binding and validation integrate with FluentValidation; data annotations can be enabled in runtime config.
- OpenAPI/Swagger targets can generate module initializer files in `obj/` to propagate document export paths at runtime.
- Serializer context generation is opt-in via `GenerateSerializerContexts=true` and writes generated source to `Generated/FastEndpoints` by default.
- Remote messaging event subscriptions derive subscriber ids by default, can use per-subscription explicit ids, or can fall back to a `RemoteConnectionCore.SubscriberID` default configured on the remote connection.

## Invariants

- Public API changes affect NuGet consumers; preserve backward compatibility unless intentionally making a breaking release.
- Multi-targeting for source packages is part of compatibility: `net8.0;net9.0;net10.0` from `Src/Directory.Build.props` except explicit overrides such as the generator.
- Strong-name signing is enabled for source and test projects through props files; avoid changing signing inputs casually.
- Keep Native AOT behavior in mind for framework, OpenAPI export, Swagger export, and serializer-context changes.
- Prefer source/test/manifest facts over prose docs when behavior is ambiguous.

## Sources

- `Src/Directory.Build.props`
- `Src/Library/FastEndpoints.csproj`
- `Src/Library/Main/MainExtensions.cs`
- `Src/Library/Endpoint/Endpoint.cs`
- `Src/Library/Endpoint/Endpoint.Setup.cs`
- `Src/Library/Endpoint/Auxiliary/EndpointDefinition.cs`
- `Src/Generator/FastEndpoints.Generator.csproj`
- `Src/Generator/FastEndpoints.Generator.targets`
- `Src/OpenApi/FastEndpoints.OpenApi.targets`
- `Src/Swagger/FastEndpoints.Swagger.targets`
- `FastEndpoints.slnx`
