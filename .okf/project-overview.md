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
Library monorepo, not a hosted product. Package inventory: [monorepo-packages.md](monorepo-packages.md).

| Area | Examples |
| --- | --- |
| Core HTTP | `FastEndpoints`, `FastEndpoints.Attributes`, `FastEndpoints.Core` |
| Messaging / jobs | `FastEndpoints.Messaging*`, `FastEndpoints.JobQueues`, `FastEndpoints.CommandRules` |
| Security / integrations | `FastEndpoints.Security`, `AspVersioning`, `OData`, `HealthChecks` |
| Docs / clients | `FastEndpoints.OpenApi`, `FastEndpoints.OpenApi.Kiota`, legacy `Swagger` / `ClientGen*` |
| Tooling / AOT | `FastEndpoints.Generator`, `FastEndpoints.Generator.Cli` |
| Testing | `FastEndpoints.Testing`, remote messaging testing |
| Addons (own version line) | `FastEndpoints.Mcp`, `FastEndpoints.A2A` (`Src/Agents/`) |

## Consumers
- App developers targeting ASP.NET Core **net8.0 / net9.0 / net10.0**
- NuGet consumers; nothing is deployed as a service from this repo

## Capabilities
- Endpoint discovery (reflection or source-generated `DiscoveredTypes`)
- FluentValidation, pre/post processors, mappers
- In-process command/event bus and gRPC remote messaging
- Job queues (storage SPI) and HTTP request idempotency (`AddIdempotency`)
- JWT/cookie auth, feature flags (`IFeatureFlag`), X402 payment helpers
- OpenAPI (Microsoft.AspNetCore.OpenApi), AOT-oriented generation

## Status
- Core version: `Src/Directory.Build.props` `<Version>` (never cite OKF)
- Agents versions: per-csproj under `Src/Agents/`
- Solutions: `FastEndpoints.slnx` (primary), `NativeAot.slnx` (AOT)

## Non-goals
- Not a hosted product
- Roadmap, sponsorship, and API catalog live in the docs site / changelog
- Public doc pages: sibling `../FE-Docs/` ([workflows.md](workflows.md))

## Glossary
| Term | Meaning |
| --- | --- |
| REPR | Request-Endpoint-Response |
| SUT / harness | Sample apps under `TestHarness/` used by integration tests |
| WAF | `WebApplicationFactory` via `FastEndpoints.Testing.AppFixture` |
| DiscoveredTypes | Source-generated type list for AOT-friendly registration |

## Sources
- `README.md`
- `Src/Directory.Build.props`
- `FastEndpoints.slnx`
