---
name: dotnet-application-architecture
description: Design, implement or review WhatHaveIDone .NET features using its RSM-derived use cases, specifications, Blazor WASM boundaries and local infrastructure conventions.
---

# WhatHaveIDone .NET architecture

Adapted from the RSM architecture skill at commit `7eb0637e06fdafc53469c66b3650db0e16af2b06`. Read [repository rules](../../../AGENTS.md) and the relevant part of the local `docs/implementation-plan.md` when present. Planning notes are intentionally excluded from Git; in a clean clone use README.md and executable tests instead. The user's accepted simplifications and project KISS rules take precedence over the source skill's general recommendations.

When infrastructure is involved, apply [shared-infra](../shared-infra/SKILL.md). Its local-copy workflow deliberately replaces RSM's submodule and upstream contribution process.

## Follow one operation end to end

- Start with the feature's entry point, application operation, domain rule and data access. Inspect an existing slice before inventing another pattern.
- Keep HTTP endpoints and UI focused on input, presentation and feedback. One meaningful state-changing request calls an intention-named MediatR use case; do not replace these with generic CRUD services or broad managers.
- Use specifications for server reads needed by a page or use case. Prefer filtering, owner scope, ordering, paging and projection in the database to loading whole collections.
- Put invariants in the domain when they must hold across operations; put operation workflow in the use case. An adapter translates a protocol rather than owning another copy of business rules.
- Add an interface or service only for a concrete boundary or shared rule. Do not make every class abstract or wrap calls solely to forward identical values.

## Use cases and execution

- Name the request `<Intention>Usecase` and keep its handler in the matching file, as in RSM. A response used only by this operation may share that file. Specifications, shared result models and other top-level types normally have separate files.
- Adapt the RSM `UsecaseHandlerBase` to explicitly validate access before execution; anonymous/system actions explicitly declare their allowed context. Keep permission checks read-only.
- Resolve the acting user from trusted server context, not request data. Load and mutate only documents owned by that user; validate ownership of referenced projects and tags too.
- Validate formal input and business rules on the server. Client validation helps presentation but cannot establish access or correctness.
- Inject read-only repositories for reads and writable repositories inside write handlers. Keep Marten/configuration details out of endpoint and page code.
- Make the transaction boundary clear. Related Marten changes, including timer switching, complete atomically. Identity EF operations have their own boundaries; do not silently assume a shared transaction with Marten.
- Preserve concurrency only where required: one running timer per user, stale edit detection, tag deduplication and repeatable imports. A read-then-write check alone does not guarantee these under concurrency.
- Add a notification only for a real current consumer. Publish completed facts after confirmed commit; do not introduce event sourcing, a bus or an outbox for unused extension points.

## Specifications and read models

- A specification is side-effect-free and represents a particular consumer's read, including owner scope and projection to its required shape.
- Place it near its server-side consumer, not in the WASM project. The client receives native API DTOs, never a repository or an IQueryable.
- Reuse a specification only when the query and result shape really match. Avoid generic load-everything specifications and overgeneralized filter frameworks.
- Never use the ignored compatibility workspace parameter as a tenant scope. The product has user ownership; a transitive tenancy library does not change that decision.

## Presentation and HTTP boundaries

- Pair `.razor` and `.razor.cs`; colocate page styles, scripts and feature-specific components. Markup stays declarative.
- Reuse the selected RSM/Lumex shell, buttons, form wrappers, detail panels and feedback before adding alternatives. Keep them in Web until another actual consumer justifies a library.
- WASM communicates through a typed native HTTP client. It does not dispatch server use cases or reference server Identity/persistence implementations.
- Adapt `AppComponentBase` to HTTP outcomes and safe API errors; retain validation feedback, cancellation and protection against updating disposed or obsolete UI. Handle authentication expiration without treating an HTML login page as a successful API response.
- Native DTOs belong in `WhatHaveIDone.API.Models` and stay browser-compatible. Toggl endpoints, DTOs/mappers and error responses belong in `WhatHaveIDone.API.Toggl`; keep its aliases, duration encoding and synthetic workspace out of the domain and frontend endpoint implementations. The single API host references the adapter for composition; the adapter references Logic, not the host or WASM.
- Use appropriate skeleton/loading, empty and failure states. End busy states in success, failure and cancellation; prevent repeated clicks where they could duplicate the actual operation.
- Show UTC instants in the user's selected time zone. Calendar rendering of midnight/DST boundaries must not mutate stored instants or split one logical record into duplicate stored entries.

## Identity and configuration

- Complete password login, SSO challenge/callback and application cookie issuance in the server host. Keep secrets and provider tokens on the server.
- Preserve relevant RSM security-stamp, lockout, antiforgery, safe redirect and 401/403 handling. Remove payroll roles, company membership and server-circuit assumptions.
- Expose only safe session/profile/bootstrap data to the client. Client authentication state is not authority for server operations.
- Resolve SSO accounts by stable provider identity, not email-only account linking. After first account creation, onboarding is explicit persistent state, not inferred from each fresh SSO session.
- API tokens authenticate the same local owners as browser sessions; the compatibility Basic scheme is not a separate user database.

## Localization and verification

- Keep user-visible text in strongly typed generated `.resx` resources owned by the relevant feature/project. Preserve placeholders across neutral English and Czech resources.
- Pass cancellation through async I/O and long loops. Use TimeProvider for time-dependent decisions; do not assume a 24-hour local calendar day.
- Test behavior that can regress: ownership, transactions/concurrency, time boundaries, wire contracts and import replay. Do not add tests that merely restate the implementation.
- Verify visual changes in the running app with screenshots or interaction recordings under `artifacts/visual-checks/`. Build/test success does not prove UI correctness.
- Update seed data, relevant documentation and plan acceptance status with the feature. Report checks that could not be run, and keep future scope out of current implementation.
