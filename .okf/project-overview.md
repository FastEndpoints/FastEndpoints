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
Library monorepo, not a hosted product. Package locations: [code-map.md](code-map.md#layout).

Runtime requirements: [dependencies.md](dependencies.md#runtime). Solutions and version lines: [monorepo-packages.md](monorepo-packages.md). Public docs source and maintenance: [workflows.md](workflows.md#public-documentation).

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
