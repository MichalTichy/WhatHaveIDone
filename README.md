# What Have I Done?!

Application foundation for a minimal personal time tracker built with .NET 10 and PostgreSQL. Every user owns their data; the product has no workspaces or company tenancy.

This layer contains the domain models, browser-compatible contracts, intention-named MediatR use cases, read specifications and the required locally copied Shared.Infrastructure projects. Business rules cover timers, time entries, projects/clients, implicit tags, reporting and repeatable imports. The following stack layers add the executable API, Web/PWA and MCP adapter.

```powershell
dotnet build WhatHaveIDone.slnx -warnaserror
dotnet test tests/WhatHaveIDone.Tests -warnaserror
```

Use the SDK pinned in global.json. All feeds are public; no source RSM checkout is needed. Shared infrastructure is copied locally, with its origin and adaptations in [SharedInfrastructure/PROVENANCE.md](SharedInfrastructure/PROVENANCE.md). Distribution rights and the final application license remain a separate release check.

Planning and verification notes in docs/ are intentionally local and excluded from Git. [AGENTS.md](AGENTS.md) and the architecture skills describe the repository conventions. The next domain feature after MVP remains a separate timesheet draft, outside confirmed TimeEntry records and reports.
