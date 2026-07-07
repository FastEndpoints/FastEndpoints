---
type: Reference
title: OKF Maintenance
description: Rules for keeping OKF synchronized and conformant.
tags: [okf, maintenance]
---

# OKF Maintenance

Preserve OKF v0.1 conformance:

- Every non-reserved `.md` file needs YAML frontmatter with a non-empty `type` field.
- `index.md` and `log.md` are reserved filenames.
- Only the bundle-root `.okf/index.md` may include frontmatter, and only for `okf_version`.
- Do not create empty placeholder files.

Update OKF when changing:

- architecture, package boundaries, project references, or module ownership;
- public APIs, endpoint behavior, route/metadata semantics, contracts, or generated docs behavior;
- binding, validation, serialization, authentication/authorization, X402, messaging, job queue, or processor behavior;
- generator outputs, generated-file locations, OpenAPI/Swagger export workflows, or Native AOT behavior;
- target frameworks, package versions, central props/targets, signing, or packaging metadata;
- build, test, pack, release, benchmark, cleanup, or local development commands;
- test layout, fixtures, runner configuration, CI filters, or required validation steps;
- repository layout, code conventions, or known gotchas.

If OKF conflicts with code/tests/config:

1. Verify current behavior from authoritative project sources.
2. Update the stale OKF file.
3. Mention the correction in the final response.

Before finishing any future task, decide whether OKF was affected. If no OKF update is needed, state that explicitly in the final response.

## Sources

- `.okf/index.md`
- Repository source, tests, manifests, and workflows listed throughout OKF.
