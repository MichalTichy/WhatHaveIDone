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

Updated the locally copied Persistence.EF.Identity package references from EF Core 10.0.6 to 10.0.13 to match the application/runtime and avoid mixed public EF relational APIs. Session factories and document/event repositories now propagate cancellation into connection opening, transaction start, session opening and commit. Failed connection opening disposes the connection. Migration call sites retain the optional token default. No upstream checkout was modified.

No root LICENSE/NOTICE/COPYING file was present in either source directory at the inspected commit. This manifest does not grant a license. Confirm distribution rights for the internally sourced components before public release; keep any subsequently supplied notices. No private feeds or credentials are copied.
