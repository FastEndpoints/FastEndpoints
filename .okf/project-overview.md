---
type: Reference
title: Project Overview
description: FastEndpoints is a REPR-pattern ASP.NET library monorepo published as multiple NuGet packages.
tags: [overview]
resource: README.md
---

# Project Overview

## Purpose
**FastEndpoints** is a developer-oriented alternative to Minimal APIs and MVC for ASP.NET Core. It implements the **REPR** pattern (Request-Endpoint-Response) with low boilerplate. Public docs: https://fast-endpoints.com

## Scope
This repo is the multi-package source for:

| Area | Packages (examples) |
| --- | --- |
| Core HTTP framework | `FastEndpoints`, `FastEndpoints.Attributes`, `FastEndpoints.Core` |
| Messaging / jobs | `FastEndpoints.Messaging*`, `FastEndpoints.JobQueues`, `FastEndpoints.CommandRules` |
| Security | `FastEndpoints.Security` |
| Docs / clients | `FastEndpoints.OpenApi`, `FastEndpoints.OpenApi.Kiota`, legacy `Swagger` / `ClientGen*` |
| Tooling / AOT | `FastEndpoints.Generator`, `FastEndpoints.Generator.Cli` |
| Testing helpers | `FastEndpoints.Testing`, remote messaging testing |
| Addons (independent versioning) | `FastEndpoints.Mcp`, `FastEndpoints.A2A` (under `Src/Agents/`) |
| Integrations | `AspVersioning`, `OData`, `HealthChecks` |

## Consumers
- Library authors and app developers targeting ASP.NET Core **net8.0 / net9.0 / net10.0**
- NuGet consumers; not an application service deployed from this repo

## Capabilities
- Endpoint discovery (reflection or source-generated `DiscoveredTypes`)
- FluentValidation integration, pre/post processors, mappers
- Command/event bus (in-process) and gRPC remote messaging
- Job queues with storage provider abstraction
- JWT/cookie auth helpers, OpenAPI (Microsoft.AspNetCore.OpenApi), AOT-oriented generation

## Status
- Library version (shared `Src/Directory.Build.props`): **8.3.0-beta.16** (verify before citing)
- Agents addons versioned separately (e.g. **1.0.0-beta.3**)
- Agents projects currently **commented out** of `FastEndpoints.slnx` but present under `Src/Agents/`
- Primary solution: `FastEndpoints.slnx`; AOT solution: `NativeAot.slnx`

## Non-goals
- Not a hosted product/API of its own
- Product roadmap, sponsorship, and full public API catalog live outside OKF (docs site / changelog)
- Public doc **pages** are maintained in sibling `../FE-Docs/` (see [workflows.md](workflows.md)); OKF only records that obligation and paths, not page content

## Glossary
| Term | Meaning |
| --- | --- |
| REPR | Request-Endpoint-Response endpoint design |
| SUT / harness | Sample apps under `TestHarness/` used by integration tests |
| WAF | `WebApplicationFactory` path via `FastEndpoints.Testing.AppFixture` |
| DiscoveredTypes | Source-generated type list for AOT-friendly registration |

## Sources
- `README.md`
- `Src/Directory.Build.props`
- `FastEndpoints.slnx`
- `Src/Library/FastEndpoints.csproj`
