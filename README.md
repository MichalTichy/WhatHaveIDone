# What Have I Done?!

A personal time tracker built with .NET 10, Blazor WebAssembly, Blazor Blueprint, PostgreSQL, and Marten. One ASP.NET Core host serves the API and the installable web app. Data is isolated by user.

The app includes account registration, single-tenant Microsoft Entra SSO, a timer, autosaving time entries, an editable calendar, projects and tags, reporting with CSV export, API keys, and background import from Toggl. The supported Toggl v9 endpoints provide compatibility for the planned Riganti IS integration.

The sidebar Feedback button opens a simple text form and a shared feedback list. All signed-in users can submit and read feedback; posts contain text and a timestamp, without account details. Any signed-in user can mark a post as resolved, which removes it from the list. Feedback is stored globally, separately from user-scoped tracking data.

## Run locally with Aspire

Install the .NET SDK pinned in [global.json](global.json), Node.js 24 with npm, Docker, and the Aspire 17 CLI. Run these commands from the repository root:

```sh
dotnet tool install --global Aspire.Cli --version 17.0.0
aspire run --apphost src/WhatHaveIDone.AppHost/WhatHaveIDone.AppHost.csproj
```

If Aspire is already installed as a global .NET tool, use `dotnet tool update --global Aspire.Cli --version 17.0.0`. Open [the application](http://localhost:5088) after the API is healthy. The CLI prints a sign-in link for the Aspire dashboard, which shows resource health, logs, HTTP traces, and metrics. Keep the dashboard sign-in link private.

Aspire starts PostgreSQL 17.6 and waits for the database before launching the API. It passes the connection string as `ConnectionStrings__DB` and keeps database data in the `whid-aspire-postgres` Docker volume. Aspire manages the PostgreSQL password through local AppHost user secrets. Press Ctrl+C to stop the AppHost. The volume persists across restarts.

The API exports OpenTelemetry data when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured. Aspire sets it during local orchestration. An external OTLP collector can supply the endpoint for deployments that use Docker Compose.

### Development sample data

A Debug build running in the Development environment seeds a demo account, projects, tags, and time entries on its first startup against the database. Sign in as `demo@example.test` using the generated password in the ignored file `src/WhatHaveIDone.API/data/demo-password.txt`. Subsequent startups preserve edits to the demo account and its data. You can also register a separate account to try the onboarding and import flow.

## Run with Docker Compose

Copy [the example environment file](.env.example) to `.env` and set a long, random `POSTGRES_PASSWORD`. Start the full application:

```sh
docker compose --profile app up -d --build --wait
```

The default URL is [http://localhost:5088](http://localhost:5088). Aspire and Compose use separate database volumes. Stop the Aspire application before starting the Compose application on the same port. Compose persists both database data and ASP.NET Core Data Protection keys. The Compose image uses a Release build. Register an account through the UI to get started.

## Build and test

With `.env` configured for Compose, run:

```sh
docker compose up -d --wait db
dotnet test WhatHaveIDone.slnx -warnaserror
node --test tests/calendar-behavior.test.mjs
dotnet publish src/WhatHaveIDone.API -c Release -o artifacts/publish -warnaserror
```

The integration suite uses the database credentials in `.env` or an explicit `WHID_TEST_CONNECTION`. Its login needs permission to create a temporary database. The suite drops its own `whid_tests_<guid>` database when it finishes. The .NET build restores the npm packages and builds the CSS and calendar assets.

## Architecture

- `WhatHaveIDone.Web` groups pages and their components under `Features`. Shared controls live in `Components`, the shell lives in `Layout`, and a typed client in `Services/Api` owns the native HTTP calls.
- `WhatHaveIDone.API` serves the frontend and native API. FastEndpoints groups HTTP operations into individual endpoint classes. `WhatHaveIDone.API.Toggl` owns compatible Toggl v9 endpoints, wire DTOs, and errors. `WhatHaveIDone.API.Models` contains contracts shared with the frontend.
- `WhatHaveIDone.Logic` exposes use cases for writes and specifications for reads. `WhatHaveIDone.Models` contains domain documents. The persistence and Identity projects provide the PostgreSQL and ASP.NET Identity integrations.
- Marten scopes tracking documents to the signed-in user. API keys are looked up globally during authentication and then resolve to their owner. The worker discovers pending imports across users and processes each run inside its owner's tenant.
- A Toggl import reads the source account and its available workspaces, then imports selected workspaces from newest to oldest. The worker saves each page and its checkpoint together and resumes after quota limits. Completed and cancelled imports discard source credentials. The UI shows up to 20 runs.
- Settings includes an opt-in month-end prediction and a working calendar. Czech holidays and weekends default to 0% capacity; individual dates support 0/25/50/75/100% overrides. `PredictionService` uses 12 weeks of daily totals with a four-week weight half-life, topping up today's expected capacity without removing recorded hours. The persistence adapter aggregates tenant-filtered intervals into local days in PostgreSQL; it returns only dates and seconds. Predictions load independently below the Time page total with their own skeleton and error state.
- `SharedInfrastructure/` contains locally copied infrastructure and the API key module with its separate Marten adapter. See the [provenance manifest](SharedInfrastructure/PROVENANCE.md) for source and licensing details.

See [repository conventions](AGENTS.md), [the .NET architecture skill](.agents/Skills/dotnet-application-architecture/SKILL.md), and [the shared infrastructure skill](.agents/Skills/shared-infra/SKILL.md). Planning and verification notes in `docs/` are local.

The UI uses Blazor Blueprint and FullCalendar, with locally served DM Sans. The font comes from [google/fonts](https://github.com/google/fonts/tree/e7db6ca603fd4862010383ff17ffd6cc4ffac1c0/ofl/dmsans) and includes its SIL Open Font License. The UI and server-side authentication flow draw on RSM commit `7eb0637e06fdafc53469c66b3650db0e16af2b06`. Timely and Timemator informed the visual design.

End-to-end verification with a real Toggl account, a configured Entra tenant, and the updated Riganti IS integration remains pending. A public open-source release also requires confirmation of distribution rights for copied internal code and selection of a project license. Timesheet drafts are the first planned feature after the MVP.

## Connect an AI client over MCP

Open **Settings → API keys → Connect AI / MCP** for setup instructions for VS Code, Cursor, and Claude Code. Configure the client with the `/mcp` URL and set `WHID_API_KEY` privately in its environment. The MCP endpoint uses Bearer authentication and runs tracking operations under the API key owner's account.

`FastEndpoints.Mcp` exposes the tracking endpoints that explicitly opt in through `McpTool`. REST and MCP use the same endpoint handlers. The MCP integration package is currently pinned to a beta version.
