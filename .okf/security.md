---
type: Security
title: Security
description: Auth helpers, endpoint authorization patterns, and signing constraints in this repo.
tags: [security]
---

# Security

## Auth surface
- **Package:** `Src/Security` → `FastEndpoints.Security` (references Library + JwtBearer).
- Helpers for JWT bearer setup, cookie auth, JWT creation/signing options, refresh tokens, revocation hooks.
- Harness wires e.g. `AddAuthenticationJwtBearer`, policies (`AdminOnly`), `UseJwtRevocation<T>()`, antiforgery middleware (`UseAntiforgeryFE`).

## Endpoint authorization
- Configured in `Configure()`: `AllowAnonymous()`, roles/permissions, policies via ASP.NET Core integration.
- `AccessControl(keyName, …)` can generate permission codes when Generator is present and optionally apply to the endpoint.
- Global security options under `Config.Security`.

## Signing / trust
- Assemblies strong-named with `FastEndpoints.snk`; public key embedded in Directory.Build.props and InternalsVisibleTo.
- Do not strip signing for “convenience” commits; breaks friend test assemblies and package expectations.

## Secrets handling
- CI publish: NuGet trusted publishing (GitHub OIDC → short-lived API key via `NuGet/login`). nuget.org username `dj-nitehawk` is set in the workflow; no API key secret.
- Sample JWT keys in harness configuration are for tests; never treat as production secrets.
- OKF and commits: names of config keys only, never values.

## Threat notes for agents
- No need to open network ports or disable auth in library code for tests; use `FastEndpoints.Testing` and test doubles.
- Remote messaging uses MessagePack over gRPC; treat as trusted-network RPC unless consumer adds auth layers.

## Sources
- `Src/Security/`
- `TestHarness/Web/Program.cs`
- `Src/Library/Config/SecurityOptions.cs`
- `Src/Directory.Build.props`
