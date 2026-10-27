# Shared.Infrastructure.AppUser

Core abstractions for the logged-in user and roles, independent of the identity provider. The concrete user type lives in the application (or in `AppUser.Identity` / `AppUser.Entra`).

## Key types

- `IUserRole`: a role with a `Name`.
- `IAppUser<TRole>`: the application user. `LoadFromClaims(IUserRoleManager<TRole>, ICollection<Claim>)` fills it from the claims of the authenticated principal.
- `IUserRoleManager<TRole>`: the set of roles the application supports (`AllRoles`, `GetExistingRole(name)`, `GetExistingRoles(names, throwOnUnknownRoles)`).
- `UserRoleManagerBase<TRole>`: base implementation. Pass the supported roles to the constructor.

## Usage

Define the application's role manager and register it as a singleton:

```csharp
public class AppUserRoleManager() : UserRoleManagerBase<UserRole>([
    UserRole._Create(AppRoles.Admin, "..."),
    UserRole._Create(AppRoles.User, "...")
]);

services.AddSingleton<IUserRoleManager<UserRole>, AppUserRoleManager>();
```

The current user is then obtained through `ICurrentUserProvider` (project `CurrentUserProvider`), which calls `LoadFromClaims`.

## Rules

- Check permissions against roles from `IUserRoleManager`, never against hard-coded role strings scattered in code; keep role names as constants in one place.
- Keep this project free of any identity-provider dependency.

## When changing this project

Every app with authentication implements these interfaces (`AppUser.Identity`, `AppUser.Entra`, `TestsBase` fakes, app user classes). Changing an interface member is a breaking change for all of them.
