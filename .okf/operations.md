---
type: Reference
title: Operations
description: CI/release automation, package publishing, benchmarks, and runtime harness notes.
tags: [operations, ci, release]
---

# Operations

## Deployment model

This repository ships NuGet packages, not a deployed service. Operational concerns center on CI validation, package packing/publishing, release notes, benchmarks, and runtime harnesses used for verification.

## CI and release

- GitHub Actions workflow `.github/workflows/publish-to-nuget.yml` runs on pushed `v*` tags.
- It checks out the repo, installs .NET 8.x/9.x/10.x SDKs, runs filtered release tests, packs `FastEndpoints.slnx`, pushes `Src/**/*.nupkg` to NuGet, and creates a GitHub release for non-beta tags using `Src/Library/changelog.md` as body.
- Azure pipeline `azure-pipeline.yml` triggers on `v*` tags and excludes branch builds. It uses .NET SDK 10.x, rewrites one integration xUnit runner config to disable parallelization, then tests `Tests/**/*.csproj` with `ExcludeInCiCd!=Yes` filter.
- Secret value names may appear in workflow files (for example NuGet API key environment wiring), but secret values must never be copied into OKF or source comments.

## Runtime harnesses

- `TestHarness/Web/` is the main ASP.NET behavior harness. It configures FastEndpoints, OpenAPI documents, authentication, authorization, X402, antiforgery, output caching, command handlers, and job queues.
- `TestHarness/NativeAotChecker/` validates Native AOT scenarios and generated output paths.
- `TestHarness/Sandbox/` provides sandbox source/contracts/tests with its own `.slnx`.

## Benchmarks and load tests

- `Benchmark/` contains BenchmarkDotNet comparisons and load tests (`FastEndpointsBench`, `MinimalApi`, `MvcControllers`, `Runner`, `LoadTests`).
- Benchmark projects target `net10.0` through `Benchmark/Directory.Build.props`.
- Treat benchmark results as validation artifacts; avoid changing benchmark harnesses casually when changing runtime code.

## Generated operational artifacts

- `bin/`, `obj/`, logs, reports, `.serena/`, and benchmark artifacts are ignored.
- Native AOT checker generated/openapi folders are ignored and should not be hand-edited.
- OpenAPI/Swagger AOT export targets can create `wwwroot/openapi` output during publish workflows.

## Sources

- `.github/workflows/publish-to-nuget.yml`
- `azure-pipeline.yml`
- `Benchmark/Directory.Build.props`
- `Benchmark/LoadTests/README.md`
- `TestHarness/Web/Program.cs`
- `.gitignore`
