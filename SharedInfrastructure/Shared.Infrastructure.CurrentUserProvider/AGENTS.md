# Shared.Infrastructure.CurrentUserProvider

Abstractions for accessing the logged-in user from services, use cases and repositories:

- `ICurrentUserProvider<TUser>`: `GetCurrentUserAsync()` (null when anonymous), `GetCurrentUserRequiredAsync()` (throws `NoLoggedUserFoundException`).
- `ICurrentUserIdProvider`: the same for the user ID only; cheaper, no role resolution.

## Implementations

| Project | Use in |
|---|---|
| `CurrentUserProvider.HttpContext` | MVC / API / DotVVM apps |
| `CurrentUserProvider.Blazor` | Blazor Server apps |
| `TestsBase` (`FakeCurrentUserProvider`, `FakeCurrentUserIdProvider`) | tests |

## Suggested use

- Check the user's roles/permissions at the beginning of each use case, using `ICurrentUserProvider<TUser>`.
- When a request contains IDs of stored objects, verify that the current user may access those objects, not just that they exist.
- Tenant isolation doesn't need extra checks: repositories use the current tenant automatically (see `CurrentTenancyProvider`).

## Rules

- Business code depends on these interfaces, never on `HttpContext`, `ClaimsPrincipal` or `AuthenticationStateProvider` directly.
- Use `GetCurrentUserRequiredAsync()` where a user must be logged in, instead of null checks that silently continue.
