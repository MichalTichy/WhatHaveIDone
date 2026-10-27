# What Have I Done?!

Application foundation for a minimal personal time tracker built with .NET 10 and PostgreSQL. Every user owns their data; the product has no workspaces or company tenancy.

This layer contains the domain models, browser-compatible contracts, intention-named MediatR use cases, read specifications and the required locally copied Shared.Infrastructure projects. Business rules cover timers, time entries, projects/clients, implicit tags, reporting and repeatable imports. The executable API is included in this layer. The following stack layers add Web/PWA and MCP.

```powershell
dotnet build WhatHaveIDone.slnx -warnaserror
dotnet test tests/WhatHaveIDone.Tests -warnaserror
```

Use the SDK pinned in global.json. All feeds are public; no source RSM checkout is needed. Shared infrastructure is copied locally, with its origin and adaptations in [SharedInfrastructure/PROVENANCE.md](SharedInfrastructure/PROVENANCE.md). Distribution rights and the final application license remain a separate release check.

Planning and verification notes in docs/ are intentionally local and excluded from Git. [AGENTS.md](AGENTS.md) and the architecture skills describe the repository conventions. The next domain feature after MVP remains a separate timesheet draft, outside confirmed TimeEntry records and reports.
## Server

ASP.NET Core hosts the native /api/v1 API and a separate Toggl v9 compatibility library under /api/v9. Identity/Entra authentication, PostgreSQL persistence, background API/CSV import, reports and owner-scoped API keys use the shared application rules.

The server requires ConnectionStrings__DB. DataProtection__Path and Import__Path should point to persistent private directories. To run PostgreSQL locally, copy .env.example to an ignored .env, replace its password and run `docker compose up -d db`.

```powershell
dotnet test WhatHaveIDone.slnx -warnaserror
dotnet run --project src/WhatHaveIDone.API
```

Integration tests accept WHID_TEST_CONNECTION and create/drop an isolated whid_tests_<guid> database. Use a development PostgreSQL account that may create databases. This layer intentionally serves the APIs without a frontend. Real Toggl/Entra/IS integration still needs configured external validation.
