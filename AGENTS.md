# Repository instructions

## Scope and source of truth

- Local planning and verification notes under `docs/` are intentionally excluded from Git. When available, read `docs/implementation-plan.md` before starting a feature, preserve accepted decisions and update it when implementation changes them. Check off work only after its acceptance criteria are met. In a clean clone, use this file, README.md, the architecture skills and executable tests as the source of truth.
- Apply [dotnet-application-architecture](.agents/Skills/dotnet-application-architecture/SKILL.md) for .NET design, implementation and architecture reviews. Apply [shared-infra](.agents/Skills/shared-infra/SKILL.md) when selecting, copying or modifying infrastructure.
- These are project-specific adaptations of RSM guidance. Explicit user decisions and this repository's conventions take precedence over inherited source conventions.
- This stack builds the application MVP; consult each layer's PR description for its implemented boundary. When available, read the local verification notes and plan for tested capabilities and pending external integration/release checks. Do not describe actual Toggl/Entra/IS integration, physical-device PWA installation or public licensing as verified when they remain pending.

## Keep it simple (KISS)

- Implement the smallest clear solution that satisfies the agreed scope. Do not build for hypothetical future requirements.
- When a small requirement introduces substantial complexity, explain the concrete cost and propose a simpler approach. Do not silently drop accepted functionality; the first release includes an editable calendar.
- Add a project, abstraction, helper, service or component split only for a concrete dependency boundary, current consumer or meaningful duplication. Do not add layers that only forward equivalent calls.
- Keep each operation and its UI state together. Extract shared behavior when there is an actual shared responsibility.
- Add notifications, hooks, retries, queues, caching and concurrency mechanisms only for actual requirements. One running timer per user, repeatable imports and protection against stale edits are actual requirements; a general event platform is not.
- Do not add infrastructure or views for future work-evidence collection to the initial release. Preserve the plan's extension boundaries without empty projects or placeholder navigation.
- The first feature after MVP is timesheet drafts. Keep draft records separate from confirmed TimeEntry documents and reports; reuse the actual time-entry rules and editor where appropriate. Do not add draft flags, workflow frameworks or empty draft projects to MVP.

## Architecture and ownership

- The API is the single server host and serves the published WASM assets. The WASM project calls the native HTTP API; it must not reference Logic, persistence, Identity implementations or Toggl adapter DTOs.
- State-changing HTTP operations call intention-named MediatR use cases. Keep business rules out of endpoints and UI. Reads use read-only repositories and specifications on the server, with filtering and projection in the database.
- Requests end in `Usecase`; keep the request and handler in the matching use-case file, following RSM. Other hand-written top-level types normally have separate files.
- Adapt RSM's handler base to require explicit access validation before execution. Resolve the actor from trusted server context and scope all reads and writes to the owner. An ID supplied by a caller is not authorization.
- There is no workspace entity or company tenancy in the product. The Toggl adapter reports workspace ID `1`, ignores the workspace route value and always scopes data by the authenticated user. A copied technical dependency on tenancy does not introduce a workspace feature.
- Clients are a small owner-scoped name list, not tenants. Each account starts with one renameable default client; projects reference one of that owner's clients. Existing and newly imported projects use the default client unless explicitly reassigned. No billing, contacts, client memberships or separate client navigation.
- Native and compatibility APIs call the same application operations. Keep protocol-specific mappings and error formats in their adapters.
- Tags are created implicitly when used on time entries, including timers and imports. Trim names, discard blank values and deduplicate/search without case sensitivity. Do not add standalone tag-creation operations or routes.
- Project badges are available immediately. Recent tag badges depend on the selected project's own history; selecting or changing a project only changes suggestions and preserves the entry's 0–N selected tags. A tag badge adds one tag, never replaces the entire selection. Cancel obsolete suggestion loads.
- `WhatHaveIDone.API.Toggl` owns the compatibility endpoints, wire DTOs and error responses. `WhatHaveIDone.API` composes it in the single host alongside the native frontend API; frontend endpoint implementations do not depend on Toggl types. The Toggl adapter references Logic, not the host or WASM.
- Complete related document writes in one Marten transaction. Identity uses its own EF Core transaction boundaries; do not imply atomicity across separate stores/contexts. Publish notifications only for real consumers and confirmed outcomes.

## UI conventions

- Reuse the selected Lumex/RSM UI foundation, including buttons, `AppComponentBase`, forms, shell, styling and feedback. Keep product-specific components in the Web project until another boundary is needed.
- Keep the personal visual direction inspired by Timely/Timemator: warm light canvas and navigation, quiet purple accent, project colours and locally hosted DM Sans. Reuse the Lumex/RSM component foundation without restoring its former corporate navy/cyan shell.
- Pair `.razor` markup with `.razor.cs`; keep page-specific styles, scripts and components nearby. Keep markup declarative.
- Adapt `AppComponentBase` to typed HTTP calls and API errors instead of server-side `ISender` and exception types. Keep cancellation, feedback and protection against updating disposed UI.
- Show skeletons where they clarify loading. End loading states on success, error or cancellation; display empty states only after a successful load.
- Use strongly typed generated `.resx` classes for user-visible text. Keep resources with their owning feature/project, with neutral English and Czech resources.
- Authentication and SSO complete on the server. Client authentication state is presentation state, never authority to access data. Do not copy Blazor Server circuit mechanisms into WASM.

## Correctness and verification

- Use UTC instants in storage and explicit user time zones for calendar layout, reporting and import. Use `TimeProvider` for business decisions involving the current time. Do not assume every local day has 24 hours.
- Pass and observe cancellation through HTTP, use cases, queries and long loops. Cancel obsolete UI loads; late responses must not overwrite a newer selection or disposed component.
- Keep authentication/session secrets on the server. Never commit credentials, real user exports or sensitive activity evidence. Keep development samples synthetic.
- Keep development seed data usable with each implemented feature. Seeding is safe to rerun and does not overwrite user changes.
- Test business behavior, ownership isolation, protocol contracts, import deduplication and meaningful concurrency/time-zone risks. Avoid tests that only mirror implementation details or copied wording.
- Build with zero warnings and run checks appropriate to the change once runnable projects exist. Report unavailable checks accurately; documentation-only work does not require starting an application.
- Verify visual changes in the running application. Inspect screenshots for static states and recordings when interactions need evidence; use an available recording skill when appropriate. Store evidence under ignored `artifacts/visual-checks/` and link it in the completion report. If the application cannot run, visual verification remains incomplete.
- Reviews focus on actionable correctness, security, performance, architecture and scope issues. Do not invent cosmetic requirements or low-value tests.
- Use comments sparingly to explain non-obvious reasons, constraints and tradeoffs, not type names or obvious behavior.

## Local infrastructure and external scope

- `SharedInfrastructure/` will contain copied projects, not a git submodule. The user explicitly authorized this arrangement. Include all required transitive projects and build assets; preserve provenance and original license/notice files.
- Do not modify the source RSM checkout or upstream Shared.Infrastructure as a side effect. Changes to the local copy are ordinary changes in this repository; no upstream branch, PR or approval workflow is implied.
- Preserve useful technical guidance from copied project `AGENTS.md` files, but adapt stale submodule, external solution and cross-repository workflow rules to this local-copy arrangement.
- Riganti IS consumer changes are a separate planned deliverable. Do not edit or deploy that repository merely to work on this application's compatibility API.
- Do not inherit RSM-specific Azure DevOps work-item transitions, payroll roles, company access rules, prototype URLs or PR automation into this project.
