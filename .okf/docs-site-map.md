---
type: Reference
title: Docs Site Map
description: Map of FastEndpoints documentation pages in the sibling FE-Docs repository.
tags: [docs, navigation, external]
---

# Docs Site Map

## Purpose

Use this map when code changes affect public docs, examples, generated documentation, or user-facing behavior. The docs source lives outside this repository at `../FE-Docs/src/content/docs/`.

## Page map

| Page | Title | Primary topics |
| --- | --- | --- |
| `01-get-started.md` | Get Started | Project setup, startup registration, request/response DTOs, endpoint classes, endpoint types. |
| `02-security.md` | Security | JWT bearer auth, cookies, endpoint authorization, security policies, claims/permissions/scopes. |
| `03-model-binding.md` | Model Binding | Binding order, JSON/form/route/query/header/claim binding, custom binders, binding attributes. |
| `04-validation.md` | Validation | FluentValidation, automatic failure responses, application validation, validator DI. |
| `05-dependency-injection.md` | Dependency Injection | Property/constructor injection, manual resolving, pre-resolved services, dependencies in validators/mappers/processors. |
| `06-domain-entity-mapping.md` | Domain Entity Mapping | Mapper classes, endpoint-local mapping, mapper dependency injection. |
| `07-file-handling.md` | File Handling | Upload binding, large uploads, file responses, response stream writing. |
| `08-response-caching.md` | Response Caching | Header-based response caching. |
| `09-rate-limiting.md` | Rate Limiting | Endpoint throttling, limits/windows, headers, reliability and limitations. |
| `10-openapi-documents.md` | OpenAPI Documents | OpenAPI configuration, endpoint descriptions, request params, XML docs, security schemes, document export. |
| `11-pre-post-processors.md` | Pre / Post Processors | Processors, short-circuiting, global processors, state sharing, ordering. |
| `12-event-bus.md` | Event Bus | Event DTOs/handlers, publishing, dependency injection, publish modes. |
| `13-command-bus.md` | Command Bus | Commands/handlers, command execution, endpoint error state, middleware pipeline. |
| `14-command-rules.md` | Command Rules | Rule input models, commands, rule registration, dispatching to immediate commands or queued jobs. |
| `15-job-queues.md` | Job Queues | Queueing, persistence, cancellation, results, progress tracking, distributed processing. |
| `16-remote-procedure-calls.md` | Remote Procedure Calls | Remote command bus, remote event queues, broker/round-robin modes, local IPC. |
| `17-server-sent-events.md` | Server Sent Events | One-way real-time SSE support. |
| `18-exception-handler.md` | Exception Handler | Unhandled exception handler, log/JSON formats, setup, customization, validation error catching. |
| `19-integration-unit-testing.md` | Integration & Unit Testing | Test setup and helper patterns. |
| `20-configuration-settings.md` | Configuration Settings | Runtime customization, JSON serializer options, naming policies, route prefixes, endpoint filtering, warmup. |
| `21-misc-conveniences.md` | Misc Conveniences | Endpoint options, verbs/routes, typed route params, endpoint properties, send methods, user context. |
| `22-api-versioning.md` | API Versioning | Release groups, versioning setup, OpenAPI release documents, endpoint versions/deprecation. |
| `23-idempotency.md` | Idempotency | Server/client setup, request uniqueness, customization, distributed cache storage. |
| `24-native-aot.md` | Native AOT | AOT prerequisites, project setup, publishing/testing, OpenAPI export. |
| `25-x402-payments.md` | x402 Payments | x402 server setup, global options, endpoint protection/overrides, settlement timing, request/response flow. |
| `26-ai-agents.md` | AI Agents | MCP tools, A2A skills, argument binding, security, testing. |
| `27-scaffolding.md` | Scaffolding | Feature and project scaffolding. |
| `28-the-cookbook.md` | The Cookbook | Recipes for auth, configuration, job persistence, middleware/pipeline, misc, results pattern. |

## Maintenance notes

- Refresh this file when docs pages are added, removed, renamed, or substantially reorganized.
- If changing public API or behavior in this repository, check the matching docs page above and update FE-Docs separately when needed.
- The FE-Docs path is outside this repository; get explicit access approval before reading or editing it in future tasks.

## Sources

- `../FE-Docs/src/content/docs/*.md`
