# Source provenance

Copied locally from RSM Shared.Infrastructure commit `cf06cd55d49d9ad9be35d84b8546c0fae375dcf1`, via RSM commit `7eb0637e06fdafc53469c66b3650db0e16af2b06`, on 2026-10-26. No submodule or reference to the source checkout is required.

## Copied projects

- `Shared.Infrastructure.AppUser`
- `Shared.Infrastructure.AppUser.Identity`
- `Shared.Infrastructure.Authentication.Entra`
- `Shared.Infrastructure.CurrentTenancyProvider`
- `Shared.Infrastructure.CurrentUserProvider`
- `Shared.Infrastructure.CurrentUserProvider.HttpContext`
- `Shared.Infrastructure.Initializer`
- `Shared.Infrastructure.IoC`
- `Shared.Infrastructure.LeaderElection`
- `Shared.Infrastructure.Persistence`
- `Shared.Infrastructure.Persistence.EF.Identity`
- `Shared.Infrastructure.Persistence.Marten`
- `Shared.Infrastructure.UIException`
- `Shared.Infrastructure.UnitOfWork`

## Adaptations and build assets

Copied dependency closure, project-local source/resources/guidance and root Directory.Build.props/targets, Directory.Packages.props, BannedSymbols.txt and editorconfig. Excluded generated bin/obj and unrelated projects. Target remains net10.0. Resolved the banned-symbol file against the props directory, enabled warnings as errors, and replaced root submodule/catalog workflows with local-copy rules. Project and namespace names are preserved.

The final dependency closure is 14 projects. Removed unused root references to UserManagement.Abstractions, DataAnnotations, Localization and IoC.Mediatr (and its otherwise unused DDD.Core dependency). Product code uses MediatR 12.5.0 directly and native generated resources. No notifications, emailing, event sourcing or company tenancy were activated.

The application now explicitly enables Marten conjoined tenancy per authenticated user through the existing tenancy/session interfaces. The shared HTTP current-user implementation reads the existing NameIdentifier claim; the application supplies the small scoped tenancy adapter, following RSM's registration pattern. No company switching, new copied projects or changes to the upstream checkouts are involved. Local root guidance reflects the user-tenant storage policy; global authentication and worker/migration queries are explicit application exceptions.

Updated the locally copied Persistence.EF.Identity package references from EF Core 10.0.6 to 10.0.13 to match the application/runtime and avoid mixed public EF relational APIs. Session factories and document/event repositories now propagate cancellation into connection opening, transaction start, session opening and commit. Failed connection opening disposes the connection. Migration call sites retain the optional token default. No upstream checkout was modified.

No root LICENSE/NOTICE/COPYING file was present in either source directory at the inspected commit. This manifest does not grant a license. Confirm distribution rights for the internally sourced components before public release; keep any subsequently supplied notices. No private feeds or credentials are copied.

The application import worker no longer injects a single-instance leader-election abstraction or registers it explicitly. LeaderElection remains in the copied transitive dependency closure through Initializer; no distributed worker-coordination feature is activated.


## Locally authored API-key authentication

Shared.Infrastructure.Authentication.ApiKey and Shared.Infrastructure.Authentication.ApiKey.Marten were authored in this repository on 2026-11-18; neither is copied from RSM or upstream Shared.Infrastructure. There are 14 copied projects plus these two local projects. The core owns generation, hashing, management and Identity-backed authentication, with an injected IApiKeyStorageService and no Marten dependency. The separate adapter supplies an internal storage service and SingleTenanted storage configuration. WhatHaveIDone keeps its existing persisted ApiToken type and native API contracts; Toggl Basic parsing stays in API.Toggl. The modules are also proposed upstream in [Shared.Infrastructure PR 20715](https://dev.azure.com/rigantitfs/Infrastructure/_git/Shared.Infrastructure/pullrequest/20715), commit `70d11db`. This copy uses the application's existing Marten 9.37.0 and build settings; the upstream contribution uses its central packages. The upstream PR is separate from the application stack and does not change the RSM submodule pointer.

The creation result is TemporaryApiKey; API keys remain valid until revoked. The Marten adapter uses the shared no-tenancy repositories and internal specifications, joining ambient transactions for application use cases. ApiKeyRecord implements the provider-neutral IItemWithId contract from Persistence.

The shared handler supports a configurable HeaderName (Authorization/Bearer by default, raw keys for custom headers) and rejects mismatched KeyPrefix values before invoking validation/storage.
