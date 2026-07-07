---
type: Reference
title: Project Overview
description: Purpose, scope, package families, and glossary for FastEndpoints.
tags: [overview, scope]
---

# Project Overview

## Purpose

FastEndpoints is a .NET library family for building ASP.NET APIs with minimal boilerplate. The main package is positioned as a developer-friendly alternative to Minimal APIs and MVC that nudges applications toward the REPR pattern: Request, Endpoint, Response.

## Scope

- Core API framework for endpoint classes, request binding, validation, response helpers, filters/processors, versioning, throttling, antiforgery, idempotency, and security metadata.
- Messaging libraries for command bus, event bus, job queues, and remote gRPC/MessagePack-style RPC helpers.
- Add-on packages for OpenAPI, legacy NSwag Swagger/client generation, OData, ASP.NET API versioning, JWT/security helpers, health checks, testing helpers, source generation, and AI protocols (MCP/A2A).
- Test harnesses and benchmarks that exercise library behavior against ASP.NET hosts.

## Consumers

- Library users building ASP.NET APIs on .NET 8+.
- Package consumers using FastEndpoints extension packages from NuGet.
- Maintainers validating behavior across unit, integration, native AOT, and benchmark projects.

## Status and maturity

- Published NuGet package family with release automation on `v*` tags.
- Current shared source package version is in `Src/Directory.Build.props`.
- Main source projects multi-target `net8.0`, `net9.0`, and `net10.0`; tests and benchmarks target `net10.0` unless overridden.

## Glossary

- **REPR**: Request-Endpoint-Response pattern used by endpoint implementations.
- **Endpoint**: A class deriving from FastEndpoints endpoint base classes and usually implementing `Configure()` plus `HandleAsync()`/`ExecuteAsync()`.
- **Processor**: Pre/post request pipeline component registered globally or per endpoint.
- **Job queue**: Command-based background queueing with scheduling/tracking support.
- **OpenApi**: New Microsoft.AspNetCore.OpenApi-based support in `Src/OpenApi/`.
- **Swagger**: Legacy NSwag-based support in `Src/Swagger/` and related legacy client generation projects.
- **Native AOT**: Ahead-of-time publish scenario validated through `NativeAot.slnx` and the native AOT harness/tests.

## Sources

- `README.md`
- `FastEndpoints.slnx`
- `NativeAot.slnx`
- `Src/Directory.Build.props`
- `Src/**/*.csproj`
