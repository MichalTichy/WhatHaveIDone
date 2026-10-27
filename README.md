# What Have I Done?!

A minimal personal time tracker built with .NET 10, Blazor WebAssembly and PostgreSQL. Every user owns their data; there are no product workspaces.

This branch is layer 3 of 13: **Add tracking use cases and read specifications**. Review its diff against the preceding layer. Feature folders and the `WhatHaveIDone.API.Models` project use their accepted final names from their introduction.

```powershell
dotnet build WhatHaveIDone.slnx -warnaserror
dotnet test WhatHaveIDone.slnx --no-build
```

## Stack contents

1. Add build conventions and vendored shared infrastructure (included)
2. Add domain documents and native API models (included)
3. Add tracking use cases and read specifications (included)
4. Add PostgreSQL persistence and ASP.NET Identity adapters (later layer)
5. Add the authenticated native HTTP API (later layer)
6. Add the Toggl v9 compatibility adapter (later layer)
7. Add resumable Toggl API and CSV imports (later layer)
8. Add the Blazor shell, shared controls and authentication UI (later layer)
9. Add time tracking, autosave editing and the editable calendar (later layer)
10. Add project management and interactive reports (later layer)
11. Add account settings and import onboarding UI (later layer)
12. Add Docker deployment, development tooling and full CI (later layer)
13. Add MCP tools and AI connection setup (later layer)

The locally copied infrastructure is documented in [SharedInfrastructure/PROVENANCE.md](SharedInfrastructure/PROVENANCE.md). Build inputs and feeds are self-contained; no RSM checkout is needed. [AGENTS.md](AGENTS.md) and the architecture skills define repository conventions. The native DTO and persisted model namespaces remain stable within feature folders.

`docs/` contains local planning and verification notes and is excluded from every commit in this stack. Real Toggl/Entra/IS integration, physical-device PWA installation and distribution rights/final licensing remain external release checks. Separate timesheet drafts remain the next domain feature; confirmed entries and reports contain no draft flag.
