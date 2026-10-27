# Shared.Infrastructure.CurrentTenancyProvider

Tenant resolution for multi-tenant apps. `ICurrentTenancyProvider.GetUserTenantAsync()` returns the current tenant ID. `Persistence.Marten` uses it (with `useTenancy: true`) to scope all repository operations.

## Key types

- `ICurrentTenancyProvider`
- `CurrentTenancyProviderNoTenancy`: default for single-tenant apps, returns `"*DEFAULT*"`.
- `DefaultSwitchableTenancyProvider`: base class for apps where privileged users (e.g. admins) may temporarily act in another tenant. Implement `GetUsersOriginalTenantAsync()` (usually from a user claim) and `CanUserUseTenancySwitchingAsync()`.
- `ITenancySwitcher`: `SwitchTenancyAsync(userId, tenantOverride)` sets or clears (null) the override. The default `TenancySwitcher` keeps overrides in memory.

## Setup

`CurrentTenancyProviderInstaller` is an `ILowPriorityInstaller` and uses `TryAdd`, so the no-tenancy default applies only when the app registers nothing. Multi-tenant apps register their own provider:

```csharp
public class AppCurrentTenancyProvider(ICurrentUserProvider<UserInfo> users, ICurrentUserIdProvider userIds, ITenancySwitcher switcher)
    : DefaultSwitchableTenancyProvider(switcher, userIds)
{
    protected override async Task<bool> CanUserUseTenancySwitchingAsync() => (await users.GetCurrentUserRequiredAsync()).IsAdmin;
    protected override async Task<string> GetUsersOriginalTenantAsync() => (await users.GetCurrentUserRequiredAsync()).CompanyId;
}

services.AddTransient<ICurrentTenancyProvider, AppCurrentTenancyProvider>();
```

## Rules

- Never pass tenant IDs from the client to repositories. The tenant comes from `ICurrentTenancyProvider`. Explicit `tenantId` arguments are only for background jobs and deliberate cross-tenant operations.
- `TenancySwitcher` is in-memory: overrides are lost on restart and not shared between instances. Provide a persistent `ITenancySwitcher` if that matters.
