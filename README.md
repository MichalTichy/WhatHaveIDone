# What Have I Done?!

A minimal personal time tracker built with .NET 10, Blazor WebAssembly and PostgreSQL. Every user owns their data; there are no product workspaces.

This branch is layer 10 of 13: **Add project management and interactive reports**. Review its diff against the preceding layer. Feature folders and the `WhatHaveIDone.API.Models` project use their accepted final names from their introduction.

```powershell
dotnet build WhatHaveIDone.slnx -warnaserror
dotnet test WhatHaveIDone.slnx --no-build
```

The API requires `ConnectionStrings__DB`. For a local database, copy `.env.example` to an ignored `.env`, set a private password and run `docker compose up -d db`. Integration tests accept `WHID_TEST_CONNECTION` and create/drop their own `whid_tests_<guid>` databases. Use a development account allowed to create databases. Data Protection keys and import files belong in private persistent directories.

The API serves the Blazor assets. Node.js 24/npm builds the CSS and calendar during the .NET build. Only the web features listed as included below exist in this intermediate layer; onboarding and the complete navigation arrive in layer 11.

## Stack contents

1. Add build conventions and vendored shared infrastructure (included)
2. Add domain documents and native API models (included)
3. Add tracking use cases and read specifications (included)
4. Add PostgreSQL persistence and ASP.NET Identity adapters (included)
5. Add the authenticated native HTTP API (included)
6. Add the Toggl v9 compatibility adapter (included)
7. Add resumable Toggl API and CSV imports (included)
8. Add the Blazor shell, shared controls and authentication UI (included)
9. Add time tracking, autosave editing and the editable calendar (included)
10. Add project management and interactive reports (included)
11. Add account settings and import onboarding UI (later layer)
12. Add Docker deployment, development tooling and full CI (later layer)
13. Add MCP tools and AI connection setup (later layer)

The locally copied infrastructure is documented in [SharedInfrastructure/PROVENANCE.md](SharedInfrastructure/PROVENANCE.md). Build inputs and feeds are self-contained; no RSM checkout is needed. [AGENTS.md](AGENTS.md) and the architecture skills define repository conventions. The native DTO and persisted model namespaces remain stable within feature folders.

`docs/` contains local planning and verification notes and is excluded from every commit in this stack. Real Toggl/Entra/IS integration, physical-device PWA installation and distribution rights/final licensing remain external release checks. Separate timesheet drafts remain the next domain feature; confirmed entries and reports contain no draft flag.
