# Agent Instructions

## OKF knowledge set

This repository uses `.okf/` as compact operational memory for AI agents.

Keep `.okf/` compliant with OKF v0.1:

- non-reserved `.md` files must start with YAML frontmatter;
- frontmatter must include a non-empty `type` field;
- `index.md` and `log.md` are reserved filenames;
- only the bundle-root `.okf/index.md` may include frontmatter, for `okf_version`.

### Before starting work

Read relevant OKF files before editing code, tests, docs, or configuration.

Start with:

- `.okf/index.md`
- `.okf/project-overview.md`
- `.okf/architecture.md`
- `.okf/code-map.md`
- `.okf/conventions.md`

Then read task-specific files, such as:

- `.okf/testing.md`
- `.okf/workflows.md`
- `.okf/dependencies.md`
- `.okf/operations.md`
- `.okf/gotchas.md`

Read only the files relevant to the task. Do not treat OKF as a replacement for checking source code, tests, manifests, or generated artifacts when exact behavior matters.

### During work

Use OKF to preserve project conventions, package boundaries, compatibility expectations, and workflows.

If OKF conflicts with source code, tests, generated artifacts, or project manifests:

1. Prefer verified current behavior from authoritative sources.
2. Update OKF to match the verified behavior.
3. Mention the correction in your final response.

### Before finishing work

Check whether your change affects OKF.

Update `.okf/` when changing:

- architecture, package boundaries, project references, or module ownership;
- public APIs, endpoints, routes, generated docs, contracts, or message formats;
- binding, validation, serialization, authentication/authorization, X402, messaging, job queues, or processor behavior;
- generated-code behavior, OpenAPI/Swagger export workflows, or Native AOT behavior;
- dependency versions, target frameworks, package metadata, signing, or package management;
- build, run, test, pack, benchmark, cleanup, generation, or release commands;
- testing strategy, test layout, fixtures, runner config, or required validation steps;
- repository layout, coding conventions, or known gotchas.

If no OKF update is needed, explicitly state why in your final response.

Do not consider the task complete until OKF is synchronized or explicitly unaffected.

## General expectations

- Keep changes focused and minimal.
- Prefer existing project patterns over new abstractions.
- Do not edit generated files or build outputs unless the task explicitly targets generation.
- Run relevant validation commands before finishing when practical.
