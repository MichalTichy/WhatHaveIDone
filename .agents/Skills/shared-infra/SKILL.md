---
name: shared-infra
description: Select, copy, wire or modify the locally vendored Shared.Infrastructure projects used by WhatHaveIDone, including dependency closure, build assets and provenance.
---

# Local Shared.Infrastructure

Adapted from RSM's shared-infra skill at commit `7eb0637e06fdafc53469c66b3650db0e16af2b06`. Read [repository rules](../../../AGENTS.md), [the copied-source manifest](../../../SharedInfrastructure/PROVENANCE.md) and the local `docs/implementation-plan.md` when present. Planning notes are intentionally excluded from Git; the manifest and project references remain available in a clean clone.

The user explicitly chose physical project copies in `SharedInfrastructure/`. There is no git submodule and no required upstream branch/PR workflow. Ordinary in-scope changes to this local copy are authorized application work; do not ask again merely because the source skill describes a submodule boundary. Do not modify the source checkout as a side effect.

## Discover before copying or implementing

1. Search the local copied catalog/project guidance if it exists. For the initial selection use the checked-out RSM source listed in the plan; read its catalog and selected project `AGENTS.md` files and linked setup READMEs.
2. Identify the capabilities actually needed by the current slice. Follow each selected `.csproj` ProjectReference recursively; add the complete required dependency closure to the application solution. Do not copy the full catalog as a default.
3. Preserve needed source files, resources, project-local guidance, licenses/notices, build properties/targets and analyzer inputs. Inspect build hooks for assumptions about the upstream solution, catalog or missing scripts.
4. Keep references relative and self-contained. A clean build must not need an absolute path, source checkout, upstream submodule or an unavailable internal feed.
5. Record the actual source repository/commit, copied project names and local adaptations in `SharedInfrastructure/PROVENANCE.md` at copy time. Namespaces and assembly names can remain unchanged.

The implemented selection has 14 projects including transitives, recorded in PROVENANCE.md. Unused UserManagement, DataAnnotations, Localization and IoC.Mediatr/DDD.Core roots were removed after verifying actual consumers. Do not copy Emailing projects: email delivery, password recovery and email verification are outside MVP. Recompute the graph when needs change; this is not a quota. A technical dependency on tenancy or leader election does not authorize corresponding product features.

## Adapt source guidance to this repository

- Preserve relevant technical requirements about repository APIs, registration, transactions, migrations, cancellation and concurrency.
- Replace submodule instructions, external PR approvals and references to the complete upstream solution with this application's local workflow. Do not leave stale nested AGENTS rules that contradict the authorized copying arrangement.
- Scope any local catalog and its checks to the copied subset. Retain a generator only when its inputs and outputs actually exist; do not keep a build target pointing at a missing upstream script.
- Do not copy upstream CI, cloud deployments, unrelated docs or scaffolding scripts without a current use. Do not require tools referenced only by the original skill when they have not been copied here.

## Use and extend the selected capabilities

- Reuse the existing repository/specification, unit-of-work, current-user, validation and initializer contracts rather than building parallel variants in product projects.
- Use the HttpContext current-user implementation for server HTTP work; do not bring the Blazor Server provider/circuit system into WASM.
- Compose provider registrations in the API host according to their actual project guidance. Referencing an assembly may trigger installers; inspect automatic discovery so optional capabilities are not accidentally enabled or registered twice.
- Startup initializers and development seeds are ordered, safe to rerun and do not overwrite user data. Wire persistence with its configuration and DB initialization, not just a project reference.
- Keep product domain types, ownership rules, use cases, application configuration and schema-dependent policies in `WhatHaveIDone.*` projects.
- Extend the smallest cohesive copied capability for a genuinely generic technical change. Add an abstraction/provider split only for a real dependency boundary; do not add projects for hypothetical providers.
- Local shared-project changes follow the application's target framework/build settings. Preserve concise project descriptions and update affected project guidance when behavior or setup changes.
- Build with zero warnings, run relevant behavior/integration tests and verify that the application solution includes all required transitive projects. Shared test fixtures are copied only when they materially help an existing test suite.
