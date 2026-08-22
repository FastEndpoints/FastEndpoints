---
type: Playbook
title: Testing
description: xUnit v3 layout, harnesses, AppFixture, and CI filter conventions.
tags: [test]
---

# Testing

## Frameworks and layout
| Piece | Detail |
| --- | --- |
| Framework | **xunit.v3** (MTP v2, no VSTest adapter), Shouldly, FakeItEasy |
| Test TFM | **net10.0** (`Tests/Directory.Build.props`) |
| Unit | `Tests/UnitTests/FastEndpoints`, `…/FastEndpoints.Testing`, `…/FastEndpoints.AspVersioning` (+ legacy Swagger unit commented in slnx) |
| Integration | `Tests/IntegrationTests/FastEndpoints` (main), OpenApi, OpenApi.Kiota, OData, Agents |
| AOT | `Tests/NativeAotTests/NativeAotCheckerTests` + `NativeAot.slnx` |
| Helpers package | `Src/Testing` → `FastEndpoints.Testing` (`AppFixture`, fixtures, Bogus) |
| Main SUT | `TestHarness/Web` (`Web.Program`) |
| Other SUTs | OData, OpenApi.Kiota, Sandbox, NativeAotChecker |

Integration projects reference harness + `FastEndpoints.Testing` + often remote messaging testing helpers.

## Commands
Root `global.json` sets `"test": { "runner": "Microsoft.Testing.Platform" }` so `dotnet test` uses MTP on the .NET 10 SDK (required after `xunit.v3` 4.0).

```bash
# Full solution tests (matches GitHub publish workflow)
dotnet test FastEndpoints.slnx -c Release --verbosity minimal --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1

# By tree (Azure pipeline workingDirectory Tests)
dotnet test Tests/**/*.csproj -c Release --filter "ExcludeInCiCd!=Yes" --max-parallel-test-modules 1

# Targeted
dotnet test Tests/UnitTests/FastEndpoints/Unit.FastEndpoints.csproj
dotnet test Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj --filter FullyQualifiedName~BindingTests
```

AOT tests: use `NativeAot.slnx` (publish workflow currently has AOT test step commented out; re-check before assuming CI runs AOT).

## Integration and data
- **TestBase serial + `[Priority]`:** `TestBase` / `TestBaseWithAssemblyFixture` apply `[TestClass(DisableParallelization = true)]`, `[TestCaseOrderer]`, and `[TestMethodOrderer]`. xunit.v3 4 default parallel mode is `Collections` (same class already serial). The `TestClass` flag is ignored in that mode and only applies if a project enables `ParallelMode.All`. Method + case orderers restore `[Priority]` across `[Fact]` methods and theory rows. No consumer attribute needed.
- **WAF caching:** `AppFixture` caches one factory per fixture type; override `OnCachedWafDisposedAsync()` for one-shot teardown of the shared factory (cached mode + `[assembly: EnableAdvancedTesting]`).
- **Sut pattern:** derive `AppFixture<Web.Program>`, override `ConfigureServices` / `SetupAsync` for clients and test doubles (`RegisterTestCommandHandler`, event receivers, etc.).
- **Auth clients:** Admin/Customer JWT obtained via login endpoints in `Sut.SetupAsync`.
- **Traits:** `[Trait("ExcludeInCiCd", "Yes")]` skips in CI (job-queue timing, some binding cases).
- **Kiota integration project:** `Int.OpenApi.Kiota` sets `IsTestingPlatformApplication=false` and `IsTestProject=false` when `CI` (GitHub) or `TF_BUILD` (Azure) is true (heavy Kiota gen; MTP keys off the former). Local `dotnet test FastEndpoints.slnx` still runs it.
- Integration runners for `FastEndpoints`, `FastEndpoints.OpenApi`, and `FastEndpoints.Agents` disable test-collection parallelization (process-wide FastEndpoints state). Azure and GitHub publish pipelines also rewrite the `FastEndpoints` runner config and pass `--max-parallel-test-modules 1` so test assemblies do not starve each other on 2-core runners.
- `Mode.WaitForAny` / `WaitForNone` offload handlers with `Task.Run`. Tests must not assert every handler side-effect immediately after those publishes; poll, or use `WaitForAll`.
- No external DB for the core suite; job storage tests use in-memory/test providers.
- Job-queue idempotency, gRPC reflection, and AOT binding/jobs live under the matching `Tests/UnitTests`, `Tests/IntegrationTests/FastEndpoints/RPCTests`, and `Tests/NativeAotTests` folders. Do not stand up a second in-process event hub with default storage types (see [gotchas.md](gotchas.md)).

## OpenAPI snapshots
- Goldens: `Tests/IntegrationTests/FastEndpoints.OpenApi/release-*.http` and `release-*.json` (plus `release-versioning-*`).
- Walker/export/versioning behavior is covered by focused tests in that project, not snapshots alone. Export mode keys live on internal `OpenApiExportMode`; public `IsExportMode` / `IsNotExportMode` (+ per-format wrappers) on `IHost` / `IHostApplicationBuilder`.
- To regenerate `.http` goldens: set `_updateSnapshots = true` in `HttpSnapshotTests.cs`, run  
  `dotnet test Tests/IntegrationTests/FastEndpoints.OpenApi/Int.OpenApi.csproj --filter FullyQualifiedName~HttpSnapshotTests`,  
  set `_updateSnapshots = false`, re-run the same filter. JSON goldens use the same flag in `SnapshotTests.cs`.

## Expectations
- New public behavior: unit tests when pure logic; integration tests against `TestHarness/Web` (or domain harness) when pipeline/HTTP involved.
- Prefer endpoint-typed client extensions over magic strings.
- Generator behavior: unit tests reference Generator project and harness where needed (`Unit.FastEndpoints` references Generator + Web).
- Keep assemblies signed consistently when using `InternalsVisibleTo` (public key in props).

## Sources
- `Tests/Directory.Build.props`
- `Src/Testing/AppFixture.Waf.cs`
- `Tests/IntegrationTests/FastEndpoints/Sut.cs`
- `Tests/IntegrationTests/FastEndpoints/Int.FastEndpoints.csproj`
- `.github/workflows/publish-to-nuget.yml`
